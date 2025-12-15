using System.Windows;
using Payroll_system.Models;

namespace Payroll_system
{
    public partial class EmployeeWindow : Window
    {
        public Employee ResultEmployee { get; private set; }

        // 1. Конструктор для создания нового
        public EmployeeWindow()
        {
            InitializeComponent();
            this.Title = "Добавить нового сотрудника";

            // Создаем НОВЫЙ объект для работы
            ResultEmployee = new Employee("", "", "");

            // Устанавливаем DataContext
            this.DataContext = ResultEmployee;
        }

        // 2. Конструктор: Перегрузка для редактирования
        public EmployeeWindow(Employee existingEmployee)
        {
            InitializeComponent(); // Вызываем InitializeComponent явно
            this.Title = "Редактировать сотрудника";

            // Назначаем ResultEmployee существующий объект (по ссылке)
            ResultEmployee = existingEmployee;

            // Устанавливаем DataContext: теперь поля XAML заполнятся данными existingEmployee
            this.DataContext = ResultEmployee;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Проверка полей через объект, т.к. XAML связан через Binding
            if (string.IsNullOrWhiteSpace(ResultEmployee.LastName) ||
                string.IsNullOrWhiteSpace(ResultEmployee.Position))
            {
                MessageBox.Show("Заполните фамилию и должность!");
                return;
            }

            // ResultEmployee уже содержит обновленные данные
            this.DialogResult = true;
        }
    }
}