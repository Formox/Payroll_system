using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Payroll_system.Models;
using Microsoft.EntityFrameworkCore.Design; // ⬅️ НОВАЯ ДИРЕКТИВА

namespace Payroll_system.Data
{
    // ====================================================================
    // 1. ФАБРИКА ДЛЯ ИНСТРУМЕНТОВ EF CORE (Обязательна для WPF)
    // ====================================================================
    // Этот класс явно указывает dotnet ef, как создать контекст для миграций.
    public class PayrollContextFactory : IDesignTimeDbContextFactory<PayrollContext>
    {
        public PayrollContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PayrollContext>();
            // Используем ту же строку подключения, что и в OnConfiguring
            optionsBuilder.UseSqlite("Data Source=payroll.db");

            return new PayrollContext(optionsBuilder.Options);
        }
    }

    // ====================================================================
    // 2. ОБНОВЛЕННЫЙ КЛАСС КОНТЕКСТА
    // ====================================================================
    public class PayrollContext : DbContext
    {
        // ⬅️ НОВЫЙ КОНСТРУКТОР: Обязателен для работы с фабрикой
        public PayrollContext(DbContextOptions<PayrollContext> options) : base(options) { }

        // ⬅️ ПУСТОЙ КОНСТРУКТОР: Добавляем для упрощения, когда контекст создается вручную
        public PayrollContext() { }

        public DbSet<Employee> Employees { get; set; }
        public DbSet<Work> Works { get; set; }
        public DbSet<CompletedWork> CompletedWorks { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payroll.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка связей для CompletedWork
            modelBuilder.Entity<CompletedWork>()
                .HasOne(cw => cw.Employee)
                .WithMany(e => e.CompletedWorks)
                .HasForeignKey(cw => cw.EmployeeId);

            modelBuilder.Entity<CompletedWork>()
                .HasOne(cw => cw.WorkItem)
                .WithMany(w => w.CompletedWorks)
                .HasForeignKey(cw => cw.WorkItemId);
        }
    }
}