using MedicalStore.DAL;
using MedicalStore.DAL.Entities;

namespace MedicalStore.BLL.Services
{
    public class ExpenseService
    {
        public List<Expense> GetAll()
        {
            using var db = new AppDbContext();
            return db.Expenses.OrderByDescending(e => e.Date).ToList();
        }

        public Expense? GetById(int id)
        {
            using var db = new AppDbContext();
            return db.Expenses.FirstOrDefault(e => e.Id == id);
        }

        public List<Expense> GetByDateRange(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.Expenses.Where(e => e.Date >= from && e.Date <= to).OrderByDescending(e => e.Date).ToList();
        }

        public decimal GetTotalExpenses(DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return db.Expenses.Where(e => e.Date >= from && e.Date <= to).Select(e => e.Amount).AsEnumerable().Sum();
        }

        public (bool Success, string Message, Expense? Expense) Create(string name, decimal amount, DateTime date, string? description)
        {
            try
            {
                using var db = new AppDbContext();
                var expense = new Expense
                {
                    Name = name,
                    Amount = amount,
                    Date = date,
                    Description = description
                };
                db.Expenses.Add(expense);
                db.SaveChanges();
                return (true, "Expense saved successfully.", expense);
            }
            catch (Exception ex)
            {
                return (false, $"Error creating expense: {ex.Message}", null);
            }
        }

        public (bool Success, string Message) Update(int id, string name, decimal amount, DateTime date, string? description)
        {
            try
            {
                using var db = new AppDbContext();
                var expense = db.Expenses.FirstOrDefault(e => e.Id == id);
                if (expense == null) return (false, "Expense not found.");

                expense.Name = name;
                expense.Amount = amount;
                expense.Date = date;
                expense.Description = description;

                db.SaveChanges();
                return (true, "Expense updated successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating expense: {ex.Message}");
            }
        }

        public (bool Success, string Message) Delete(int id)
        {
            try
            {
                using var db = new AppDbContext();
                var expense = db.Expenses.FirstOrDefault(e => e.Id == id);
                if (expense == null) return (false, "Expense not found.");

                db.Expenses.Remove(expense);
                db.SaveChanges();
                return (true, "Expense deleted successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting expense: {ex.Message}");
            }
        }
    }
}
