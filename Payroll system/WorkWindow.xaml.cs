using System.Windows;
using Payroll_system.Models;
using System;
using System.Globalization;

namespace Payroll_system
{
    public partial class WorkWindow : Window
    {
        public Work ResultWork { get; private set; }

        // 1. Конструктор для добавления новой работы
        public WorkWindow()
        {
            InitializeComponent();
            this.Title = "Добавить новый вид работы";

            
            cbType.ItemsSource = Enum.GetValues(typeof(WorkType)).Cast<WorkType>();
            cbType.SelectedIndex = 0; // Выбираем первый элемент по умолчанию

            // Устанавливаем стратегию по умолчанию
            rbHourly.IsChecked = true;
        }

        // 2. Конструктор для редактирования существующей работы
        // (Этот конструктор уже выглядит правильно)
        public WorkWindow(Work existingWork) : this() // Вызывает конструктор 1 для инициализации
        {
            this.Title = "Редактировать вид работы";

            // Сохраняем ссылку на объект для редактирования
            ResultWork = existingWork;

            // Заполнение полей данными из существующей работы
            // Это должно работать, если ItemsSource установлено в конструкторе 1 (this())
            cbType.SelectedItem = existingWork.Type;
            txtDescription.Text = existingWork.Description;
            // Используем N2 для вывода в формате с двумя знаками после запятой
            txtRate.Text = existingWork.HourlyRate.ToString("N2", CultureInfo.InvariantCulture);

            // !!! ИСПРАВЛЕНИЕ ОШИБКИ: используем GetStrategy() вместо Strategy
            ISalaryStrategy currentStrategy = existingWork.GetStrategy();

            // Проверка и установка правильной стратегии
            if (currentStrategy is BonusPercentStrategy bps)
            {
                rbBonus.IsChecked = true;
                txtBonusPercent.Text = bps.Percent.ToString("N2", CultureInfo.InvariantCulture);
            }
            else // HourlyStrategy
            {
                rbHourly.IsChecked = true;
            }

            ResultWork = existingWork;
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Сбор данных
                if (cbType.SelectedItem == null || string.IsNullOrWhiteSpace(txtDescription.Text))
                {
                    MessageBox.Show("Заполните все основные поля.", "Ошибка");
                    return;
                }

                WorkType type = (WorkType)cbType.SelectedItem;
                string description = txtDescription.Text.Trim();

                // Используем InvariantCulture для надежного парсинга double
                if (!double.TryParse(txtRate.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double rate) || rate <= 0)
                {
                    MessageBox.Show("Неверное значение ставки.", "Ошибка");
                    return;
                }

                // 2. Определение стратегии
                ISalaryStrategy strategy;
                if (rbBonus.IsChecked == true)
                {
                    if (!double.TryParse(txtBonusPercent.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double percent) || percent < 0)
                    {
                        MessageBox.Show("Неверное значение процента надбавки.", "Ошибка");
                        return;
                    }
                    strategy = new BonusPercentStrategy(percent);
                }
                else // rbHourly.IsChecked == true
                {
                    strategy = new HourlyStrategy();
                }

                // 3. Создание или обновление объекта Work
                if (ResultWork == null)
                {
                    // Создаем новый объект Work
                    ResultWork = new Work(type, description, rate, strategy);
                }
                else
                {
                    // Обновляем существующий объект Work
                    ResultWork.Type = type;
                    ResultWork.Description = description;
                    ResultWork.HourlyRate = rate;
                    // Здесь мы не можем просто присвоить ResultWork.Strategy = strategy, 
                    // так как свойство Strategy удалено.
                    // Вместо этого, конструктор Work сам сохранит необходимые данные (StrategyTypeKey и BonusPercentValue).
                    // Для обновления мы создадим новый объект Work с теми же данными
                    // и заменим им ResultWork в MainWindow.

                    // Создаем временную работу с новой стратегией
                    var tempWork = new Work(type, description, rate, strategy);
                    // Копируем данные из tempWork в ResultWork
                    ResultWork.Type = tempWork.Type;
                    ResultWork.Description = tempWork.Description;
                    ResultWork.HourlyRate = tempWork.HourlyRate;
                    ResultWork.StrategyTypeKey = tempWork.StrategyTypeKey;
                    ResultWork.BonusPercentValue = tempWork.BonusPercentValue;
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла непредвиденная ошибка: {ex.Message}", "Критическая ошибка");
            }
        }
    }
}