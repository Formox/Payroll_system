using System.Collections.Generic;

namespace Payroll_system.Models
{
    // Класс для сохранения всего состояния программы
    public class DataContainer
    {
        // Используем List<T> вместо ObservableCollection<T> для простой сериализации
        public List<Work> Works { get; set; } = new List<Work>();
        public List<Employee> Employees { get; set; } = new List<Employee>();
    }
}