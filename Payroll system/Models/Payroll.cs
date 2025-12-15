using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization; // Для игнорирования свойств при JSON-сериализации

namespace Payroll_system.Models
{
    // =========================================================================
    // 1. СТРАТЕГИИ
    // =========================================================================

    public enum WorkType { Office, Field, Technical, Managerial, Creative }

    // Интерфейс стратегии
    public interface ISalaryStrategy
    {
        double Calculate(double rate, double hours);
        string GetName();
        // Добавление этого свойства позволяет определить тип стратегии
        string StrategyId { get; }
    }
    public interface IPrintable
    {
        string GetFormattedString();
    }

    public interface IValidatable
    {
        bool Validate();
    }
    public class HourlyStrategy : ISalaryStrategy
    {
        public double Calculate(double rate, double hours) => rate * hours;
        public string GetName() => "Без надбавок";
        [JsonIgnore]
        public string StrategyId => "Hourly";
    }

    public class BonusPercentStrategy : ISalaryStrategy
    {
        public double Percent { get; set; }
        public BonusPercentStrategy(double p) => Percent = p;
        public BonusPercentStrategy() { } // ПУСТОЙ КОНСТРУКТОР ДЛЯ JSON/EF

        public double Calculate(double rate, double hours) => (rate * hours) * (1 + Percent / 100.0);
        public string GetName() => $"Надбавка {Percent:N2}%";
        [JsonIgnore]
        public string StrategyId => "Bonus";
    }

    // =========================================================================
    // 2. КЛАСС WORK (ВИД РАБОТЫ)
    // =========================================================================
    public class Work
    {
        // НОВОЕ: ПЕРВИЧНЫЙ КЛЮЧ ДЛЯ EF CORE
        public int Id { get; set; }

        // ОСНОВНЫЕ СВОЙСТВА
        public WorkType Type { get; set; }
        public string Description { get; set; }
        public double HourlyRate { get; set; }

        // СВОЙСТВА ДЛЯ ПЕРСИСТЕНТНОСТИ СТРАТЕГИИ
        public string StrategyTypeKey { get; set; } // Например, "Hourly" или "Bonus"
        public double? BonusPercentValue { get; set; } // Хранит процент для BonusStrategy

        // НОВОЕ: НАВИГАЦИОННОЕ СВОЙСТВО ДЛЯ EF CORE (ICollection вместо ObservableCollection)
        public ICollection<CompletedWork> CompletedWorks { get; set; } = new List<CompletedWork>();

        // Обязательный конструктор для EF Core/JSON
        public Work() { }

        public Work(WorkType type, string description, double hourlyRate, ISalaryStrategy strategy)
        {
            Type = type;
            Description = description;
            HourlyRate = hourlyRate;

            // Логика для сохранения стратегии
            if (strategy is BonusPercentStrategy bps)
            {
                StrategyTypeKey = "Bonus";
                BonusPercentValue = bps.Percent;
            }
            else // HourlyStrategy
            {
                StrategyTypeKey = "Hourly";
                BonusPercentValue = 0.0;
            }
        }

        // МЕТОД: Восстановление стратегии из сохраненных полей
        [JsonIgnore]
        public string TypeDisplay => Type.ToString(); // Возвращает "Office", "Field" и т.д.
        public string StrategyDisplay => GetStrategy().GetName();
        public ISalaryStrategy GetStrategy()
        {
            // 🔴 ИЗМЕНЕНИЕ: Безопасное чтение BonusPercentValue с использованием оператора ??
            if (StrategyTypeKey == "Bonus")
            {
                // Если BonusPercentValue == null, используем 0.0 (на всякий случай)
                double percent = BonusPercentValue ?? 0.0;
                return new BonusPercentStrategy(percent);
            }
            return new HourlyStrategy();
        }
    }

    // =========================================================================
    // 3. КЛАСС EMPLOYEE (СОТРУДНИК)
    // =========================================================================
    public class Employee : IPrintable, IValidatable
    {
        // НОВОЕ: ПЕРВИЧНЫЙ КЛЮЧ ДЛЯ EF CORE
        public int Id { get; set; }

        // Свойства данных
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Position { get; set; }

        // НОВОЕ: НАВИГАЦИОННОЕ СВОЙСТВО ДЛЯ EF CORE
        public ICollection<CompletedWork> CompletedWorks { get; set; } = new List<CompletedWork>();

        // Обязательный конструктор для EF CORE/JSON
        public Employee() { }

        // Основной конструктор
        public Employee(string last, string first, string pos)
        {
            LastName = last;
            FirstName = first;
            Position = pos;
        }

        // Расчетные свойства (игнорируются БД)
        public double TotalSalary => CompletedWorks.Sum(w => w.CalculateCost());
        public string TotalSalaryDisplay => TotalSalary.ToString("N2") + " руб.";

        public override string ToString() => $"{LastName} {FirstName} ({Position})";
        public string GetFormattedString()
        {
            return $"Сотрудник: {LastName} {FirstName}, Должность: {Position}";
        }

        // Метод интерфейса IValidatable
        public bool Validate()
        {
            return !string.IsNullOrWhiteSpace(LastName) &&
                   !string.IsNullOrWhiteSpace(FirstName);
        }
    }

    // =========================================================================
    // 4. КЛАСС COMPLETEDWORK (ВЫПОЛНЕННАЯ РАБОТА)
    // =========================================================================
    public class CompletedWork
    {
        // НОВОЕ: ПЕРВИЧНЫЙ КЛЮЧ ДЛЯ EF CORE
        public int Id { get; set; }

        // НОВОЕ: ВНЕШНИЕ КЛЮЧИ (Обязательно для связи в БД)
        public int EmployeeId { get; set; }
        public int WorkItemId { get; set; }

        // ОСНОВНЫЕ СВОЙСТВА
        public double Hours { get; set; }

        // НАВИГАЦИОННЫЕ СВОЙСТВА
        public Employee Employee { get; set; } // Связь с сотрудником
        public Work WorkItem { get; set; } // Связь с видом работы

        // Обязательный конструктор для EF Core/JSON
        public CompletedWork() { }

        // Расчет стоимости
        public double CalculateCost()
        {
            // Проверка на null важна при работе с EF Core (пока не загружена WorkItem)
            if (WorkItem == null) return 0.0;
            return WorkItem.GetStrategy().Calculate(WorkItem.HourlyRate, Hours);
        }

        // Свойства для отображения в DataGrid
        [JsonIgnore]
        public double Cost => CalculateCost(); // Cost теперь просто вызывает CalculateCost

        [JsonIgnore]
        public string WorkDescription => WorkItem?.Description ?? "Удаленная работа";

        [JsonIgnore]
        public string StrategyUsed => WorkItem?.GetStrategy()?.GetName() ?? "Неизвестно";

        [JsonIgnore]
        public string CostDisplay => Cost.ToString("N2") + " руб.";
    }
}