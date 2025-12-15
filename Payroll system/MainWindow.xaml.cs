using System.Collections.ObjectModel;
using System.Windows;
using System.Text.Json.Serialization;
using Payroll_system.Models;
using Payroll_system.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Text.Json;

namespace Payroll_system
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<Work> Works { get; set; }
        public ObservableCollection<Employee> Employees { get; set; }

        // Контекст базы данных
        private PayrollContext _context;

        public MainWindow()
        {
            InitializeComponent();

            // Инициализация коллекций для WPF-привязки
            Works = new ObservableCollection<Work>();
            Employees = new ObservableCollection<Employee>();

            // Привязка данных к DataGrid
            dgWorks.ItemsSource = Works;
            dgEmployees.ItemsSource = Employees;

            // 1. Инициализация контекста
            _context = new PayrollContext();

            // 🔴 1. ПРИНУДИТЕЛЬНОЕ СОЗДАНИЕ БД И СХЕМЫ
            _context.Database.EnsureCreated();

            // 🔴 2. ЗАПОЛНЕНИЕ ДЕФОЛТНЫМИ ДАННЫМИ (Это гарантирует наличие таблиц и данных)
            InitializeDefaultData(); // <--- ЭТОТ ВЫЗОВ ДОЛЖЕН БЫТЬ ПЕРВЫМ ИЗ ДВУХ

            // 🔴 3. ЗАГРУЗКА ДАННЫХ ДЛЯ UI (Теперь мы уверены, что таблица Works существует и в ней есть данные)
            LoadDataFromDatabase();

            CalculateSummary();
        }

        // Метод для загрузки данных из БД в коллекции WPF
        private void LoadDataFromDatabase()
        {
            // 1. Загрузка видов работ
            Works.Clear();
            // Виды работ загружаем без .Include(CompletedWorks), чтобы избежать циклических ссылок
            var worksFromDb = _context.Works.ToList();
            foreach (var work in worksFromDb)
            {
                Works.Add(work);
            }

            // 2. Загрузка сотрудников
            Employees.Clear();

            // 🔴 КРИТИЧЕСКИЙ ШАГ: Используем ThenInclude для глубокой загрузки WorkItem
            var employeesFromDb = _context.Employees
                .Include(e => e.CompletedWorks)
                .ThenInclude(cw => cw.WorkItem) // <--- ЭТО ГАРАНТИРУЕТ, что WorkItem (с данными стратегии) загружен
                .ToList();

            foreach (var employee in employeesFromDb)
            {
                // 🔴 НЕОБХОДИМЫЙ ШАГ: Принудительная инициализация стратегии!
                // Проходим по всем выполненным работам сотрудника
                foreach (var completedWork in employee.CompletedWorks)
                {
                    // Этот вызов заставляет WorkItem.GetStrategy() сработать.
                    // Без этого вызова стратегия не будет создана до первого обращения к TotalSalary.
                    completedWork.CalculateCost();
                }

                Employees.Add(employee);
            }
        }

        // Заполнение дефолтными данными, если база данных пуста
        private void InitializeDefaultData()
        {
            // Проверяем, есть ли хотя бы одна запись в таблице Works
            if (!_context.Works.Any())
            {
                var work1 = new Work(WorkType.Office, "Написание отчета", 500.0, new HourlyStrategy());
                var work2 = new Work(WorkType.Technical, "Настройка сервера", 1200.0, new BonusPercentStrategy(5.0));

                _context.Works.AddRange(work1, work2);

                // 🔴 НОВОЕ: Принудительное сохранение работ, чтобы их ID стали доступны
                _context.SaveChanges();

                // Добавляем в коллекцию WPF
                Works.Add(work1);
                Works.Add(work2);
            }

            // Проверяем, есть ли хотя бы одна запись в таблице Employees
            if (!_context.Employees.Any())
            {
                var firstWork = _context.Works.FirstOrDefault();

                var emp1 = new Employee("Иванов", "Иван", "Менеджер");

                if (firstWork != null)
                {
                    var completedWork = new CompletedWork() { WorkItem = firstWork, Hours = 8 };
                    emp1.CompletedWorks.Add(completedWork);
                }

                _context.Employees.Add(emp1);
                _context.SaveChanges();
                Employees.Add(emp1);
            }

            // Сохраняем все дефолтные данные одним пакетом
            if (!_context.ChangeTracker.Entries().All(e => e.State == EntityState.Unchanged))
            {
                _context.SaveChanges();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CalculateSummary();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Корректное закрытие подключения к БД
            _context?.Dispose();
        }

        private void CalculateSummary()
        {
            // Общая сумма выплат
            double totalPayments = Employees.Sum(e => e.TotalSalary);
            txtTotalPayments.Text = $"Общая сумма выплат: {totalPayments:N2} руб.";

            // Средняя ставка по всем видам работ
            if (Works.Any())
            {
                double averageRate = Works.Average(w => w.HourlyRate);
                txtAverageRate.Text = $"Средняя ставка по всем видам работ: {averageRate:N2} руб./час";
            }
            else
            {
                txtAverageRate.Text = "Средняя ставка по всем видам работ: Нет данных.";
            }
        }

        // =========================================================================
        // ЛОГИКА РАБОТЫ С ВИДАМИ РАБОТ (Work)
        // =========================================================================

        // --- Works Tab: ДОБАВИТЬ ---
        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            WorkWindow workWindow = new WorkWindow();

            if (workWindow.ShowDialog() == true)
            {
                _context.Works.Add(workWindow.ResultWork);
                _context.SaveChanges();

                Works.Add(workWindow.ResultWork);
                CalculateSummary();
            }
        }

        // --- Works Tab: РЕДАКТИРОВАТЬ ---
        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            if (dgWorks.SelectedItem is Work selectedWork)
            {
                WorkWindow workWindow = new WorkWindow(selectedWork);

                if (workWindow.ShowDialog() == true)
                {
                    // EF Core автоматически отслеживает изменения в selectedWork
                    _context.SaveChanges();

                    dgWorks.Items.Refresh();
                    CalculateSummary();
                }
            }
            else
            {
                MessageBox.Show("Выберите вид работы для редактирования.", "Внимание");
            }
        }

        // --- Works Tab: УДАЛИТЬ ---
        private void DeleteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (dgWorks.SelectedItem is Work selectedWork)
            {
                if (MessageBox.Show($"Вы уверены, что хотите удалить работу '{selectedWork.Description}'?",
                                    "Подтверждение удаления", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _context.Works.Remove(selectedWork);
                    _context.SaveChanges();

                    Works.Remove(selectedWork);
                    CalculateSummary();
                }
            }
            else
            {
                MessageBox.Show("Выберите вид работы для удаления.", "Внимание");
            }
        }

        // --- Works Tab: СОРТИРОВКА ---
        private void SortBtn_Click(object sender, RoutedEventArgs e)
        {
            var sortedWorks = Works.OrderBy(w => w.HourlyRate).ToList();
            Works.Clear();
            foreach (var work in sortedWorks)
            {
                Works.Add(work);
            }

            MessageBox.Show("Список видов работ отсортирован по ставке (Hourly Rate).", "Сортировка завершена");
        }

        // --- Works Tab: СОХРАНИТЬ В ФАЙЛ (Экспорт) ---
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = "works_backup.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. Получаем все работы из базы данных
                    var worksToSave = _context.Works.AsNoTracking().ToList();

                    // 2. Настройки для форматирования JSON
                    // 🔴 ИЗМЕНЕНИЕ: Добавляем ReferenceHandler.Preserve для игнорирования циклических ссылок
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve // <--- ВОТ ЧТО НУЖНО
                    };

                    string jsonString = JsonSerializer.Serialize(worksToSave, options);

                    File.WriteAllText(saveFileDialog.FileName, jsonString);
                    MessageBox.Show($"Данные успешно сохранены в файл: {saveFileDialog.FileName}", "Сохранение завершено");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- Works Tab: ЗАГРУЗИТЬ ИЗ ФАЙЛА (Импорт) ---
        private void LoadBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);

                    // Настройки десериализации (должны совпадать с тем, как сохраняли)
                    var options = new JsonSerializerOptions
                    {
                        ReferenceHandler = ReferenceHandler.Preserve,
                        WriteIndented = true
                    };

                    // 1. Десериализуем данные во ВРЕМЕННЫЙ список
                    var loadedEmployees = JsonSerializer.Deserialize<List<Employee>>(json, options);

                    if (loadedEmployees == null || loadedEmployees.Count == 0)
                    {
                        MessageBox.Show("Файл пуст или имеет неверный формат.");
                        return;
                    }

                    // Загружаем все существующие работы в память для быстрого поиска, чтобы не создавать дубликаты
                    // Используем Local, чтобы видеть и только что добавленные, но еще не сохраненные работы
                    _context.Works.Load();
                    var existingWorksCache = _context.Works.Local.ToObservableCollection();

                    int addedEmployees = 0;
                    int addedWorks = 0;

                    foreach (var loadedEmp in loadedEmployees)
                    {
                        // --- ШАГ 1: Поиск или создание Сотрудника ---

                        // Ищем сотрудника в БД по ФИО
                        var dbEmp = _context.Employees
                            .Include(e => e.CompletedWorks)
                            .FirstOrDefault(e => e.LastName == loadedEmp.LastName && e.FirstName == loadedEmp.FirstName);

                        Employee targetEmployee;

                        if (dbEmp != null)
                        {
                            // Сотрудник уже есть — используем его
                            targetEmployee = dbEmp;
                        }
                        else
                        {
                            // Сотрудника нет — добавляем как нового
                            // ВАЖНО: Сбрасываем Id в 0, чтобы БД присвоила новый
                            loadedEmp.Id = 0;

                            // Очищаем работы при добавлении сотрудника, мы добавим их корректно ниже
                            // (чтобы избежать конфликтов ID во вложенных объектах)
                            var worksTemp = loadedEmp.CompletedWorks.ToList();
                            loadedEmp.CompletedWorks = new List<CompletedWork>();

                            _context.Employees.Add(loadedEmp);
                            targetEmployee = loadedEmp;

                            // Восстанавливаем список для обработки в цикле ниже
                            foreach (var w in worksTemp) loadedEmp.CompletedWorks.Add(w);

                            addedEmployees++;
                        }

                        // --- ШАГ 2: Обработка выполненных работ ---

                        // Создаем копию списка, так как будем модифицировать коллекцию
                        var importedWorks = loadedEmp.CompletedWorks.ToList();

                        foreach (var importedCompletedWork in importedWorks)
                        {
                            // Нам нужно найти реальный объект Work в нашей базе, соответствующий загруженному
                            var workDescription = importedCompletedWork.WorkItem?.Description;

                            if (string.IsNullOrEmpty(workDescription)) continue;

                            // Ищем работу в кэше (Local) или в БД
                            var dbWork = _context.Works.Local
                                .FirstOrDefault(w => w.Description == workDescription);

                            if (dbWork == null)
                            {
                                // Если такого вида работы нет вообще — создаем новый
                                dbWork = importedCompletedWork.WorkItem;
                                dbWork.Id = 0; // Сбрасываем ID, это новая работа
                                _context.Works.Add(dbWork);
                                addedWorks++;
                            }

                            // --- ШАГ 3: Проверка на дубликат записи о работе ---
                            // Проверяем, не назначена ли уже эта работа этому сотруднику (те же часы, та же работа)
                            // Для нового сотрудника (targetEmployee.Id == 0) этот список пока пуст в БД, но может быть заполнен в памяти
                            bool alreadyExists = false;

                            if (targetEmployee.CompletedWorks != null)
                            {
                                alreadyExists = targetEmployee.CompletedWorks.Any(cw =>
                                    cw.WorkItem != null &&
                                    cw.WorkItem.Description == dbWork.Description &&
                                    Math.Abs(cw.Hours - importedCompletedWork.Hours) < 0.001);
                            }

                            if (!alreadyExists)
                            {
                                // Создаем новую запись связи "Сотрудник-Работа"
                                var newRecord = new CompletedWork
                                {
                                    Id = 0, // Новый ID
                                    Hours = importedCompletedWork.Hours,
                                    Employee = targetEmployee, // Привязываем к правильному сотруднику
                                    WorkItem = dbWork          // Привязываем к правильной работе
                                };

                                // Если список null (бывает при создании нового), инициализируем
                                if (targetEmployee.CompletedWorks == null)
                                    targetEmployee.CompletedWorks = new List<CompletedWork>();

                                targetEmployee.CompletedWorks.Add(newRecord);
                            }
                        }
                    }

                    // 3. Сохраняем все изменения одним махом
                    _context.SaveChanges();

                    // 4. Обновляем экран (вызываем ваш метод загрузки)
                    LoadDataFromDatabase();

                    MessageBox.Show($"Данные успешно объединены!\nНовых сотрудников: {addedEmployees}\nНовых видов работ: {addedWorks}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке: {ex.Message}\n{ex.InnerException?.Message}");
                }
            }
        }

        // =========================================================================
        // ЛОГИКА РАБОТЫ СО СЛУЖАЩИМИ (Employee)
        // =========================================================================

        // --- Employees Tab: ДОБАВИТЬ ---
        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            EmployeeWindow win = new EmployeeWindow();
            if (win.ShowDialog() == true)
            {
                _context.Employees.Add(win.ResultEmployee);
                _context.SaveChanges();

                Employees.Add(win.ResultEmployee);
                CalculateSummary();
            }
        }

        // --- Employees Tab: УДАЛИТЬ ---
        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (dgEmployees.SelectedItem is Employee selected)
            {
                if (MessageBox.Show($"Вы уверены, что хотите удалить сотрудника '{selected.LastName} {selected.FirstName}'?",
                                    "Подтверждение удаления", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    _context.Employees.Remove(selected);
                    _context.SaveChanges();

                    Employees.Remove(selected);
                    CalculateSummary();
                }
            }
            else
            {
                MessageBox.Show("Выберите сотрудника для удаления.");
            }
        }

        // --- Employees Tab: НАЗНАЧИТЬ РАБОТУ ---
        private void AssignWork_Click(object sender, RoutedEventArgs e)
        {
            if (dgEmployees.SelectedItem is Employee selectedEmployee)
            {
                // ...
                AssignWorkWindow assignWin = new AssignWorkWindow(selectedEmployee, Works);
                if (assignWin.ShowDialog() == true)
                {
                    // CompletedWork добавлена в коллекцию selectedEmployee.CompletedWorks
                    _context.SaveChanges(); // <-- EF Core должен увидеть новый CompletedWork через selectedEmployee

                    selectedEmployee.CompletedWorks.Add(assignWin.ResultCompletedWork);
                    dgEmployees.Items.Refresh(); // Обновляет расчет TotalSalaryDisplay
                    CalculateSummary();
                }
            }
            else
            {
                MessageBox.Show("Выберите сотрудника, которому нужно назначить работу.");
            }
        }

        // --- Employees Tab: ПОКАЗАТЬ РАБОТЫ ---
        private void ShowEmployeeWorks_Click(object sender, RoutedEventArgs e)
        {
            if (dgEmployees.SelectedItem is Employee selectedEmployee)
            {
                string details = $"Выполненные работы сотрудника {selectedEmployee.LastName} {selectedEmployee.FirstName}:\n";

                if (selectedEmployee.CompletedWorks.Count == 0)
                {
                    details += "Работы не назначены.";
                }
                else
                {
                    foreach (var cw in selectedEmployee.CompletedWorks)
                    {
                        // Предполагается, что WorkItem был загружен через ThenInclude
                        details += $"- {cw.WorkDescription} ({cw.Hours} ч.) = {cw.CostDisplay}\n";
                    }
                }
                details += $"\nИТОГ ЗАРПЛАТА: {selectedEmployee.TotalSalaryDisplay}";
                MessageBox.Show(details, $"Работы {selectedEmployee.LastName}");
            }
            else
            {
                MessageBox.Show("Выберите сотрудника, чтобы увидеть его работы.");
            }
        }

        // --- Employees Tab: РЕДАКТИРОВАТЬ ---
        private void EditEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (dgEmployees.SelectedItem is Employee selectedEmployee)
            {
                EmployeeWindow employeeWindow = new EmployeeWindow(selectedEmployee);

                if (employeeWindow.ShowDialog() == true)
                {
                    _context.SaveChanges();
                    dgEmployees.Items.Refresh();
                    CalculateSummary();
                }
            }
            else
            {
                MessageBox.Show("Выберите сотрудника для редактирования.", "Внимание");
            }
        }
    }
}