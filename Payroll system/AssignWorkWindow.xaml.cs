using System.Collections.ObjectModel;
using System.Windows;
using Payroll_system.Models;

namespace Payroll_system
{
    public partial class AssignWorkWindow : Window
    {
        public CompletedWork ResultCompletedWork { get; private set; }

        // Конструктор принимает текущего сотрудника и список доступных работ
        public AssignWorkWindow(Employee employee, ObservableCollection<Work> availableWorks)
        {
            InitializeComponent();
            txtEmployeeName.Text = $"Назначение для: {employee.ToString()}";
            // Привязываем список доступных работ к ComboBox
            cbWorkItem.ItemsSource = availableWorks;
        }

        private void Assign_Click(object sender, RoutedEventArgs e)
        {
            // Проверка ввода
            if (cbWorkItem.SelectedItem is Work selectedWork &&
                double.TryParse(txtHours.Text, out double hours) && hours > 0)
            {
                ResultCompletedWork = new CompletedWork
                {
                    WorkItem = selectedWork,
                    Hours = hours
                };
                this.DialogResult = true;
            }
            else
            {
                MessageBox.Show("Проверьте, что выбрана работа и часы введены корректно (число > 0).");
            }
        }
    }
}