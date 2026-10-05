using PanelPracownika.Models;

namespace PanelPracownika.Services
{
    public static class SalaryCalculation
    {
        public static string? ValidateSecondaryContract(UserSalary salary)
        {
            if (!salary.HasSecondaryContract) return null;
            if (salary.ContractType != "Umowa zlecenie")
                return "Podział godzin między dwie umowy jest dostępny dla umowy zlecenie.";
            if (salary.HourlyRate == null || !double.IsFinite(salary.HourlyRate.Value) || salary.HourlyRate < 0 || salary.HourlyRate > 1000000)
                return "Uzupełnij poprawną stawkę godzinową głównej umowy.";
            if (salary.SecondaryHourlyRate == null || !double.IsFinite(salary.SecondaryHourlyRate.Value) || salary.SecondaryHourlyRate <= 0 || salary.SecondaryHourlyRate > 1000000)
                return "Stawka drugiej umowy musi być większa od zera i nie większa niż 1 000 000 zł.";
            if (salary.SecondaryMonthlyHours == null || !double.IsFinite(salary.SecondaryMonthlyHours.Value) || salary.SecondaryMonthlyHours <= 0 || salary.SecondaryMonthlyHours > 744)
                return "Liczba godzin drugiej umowy musi być większa od zera i nie większa niż 744.";
            return null;
        }

        public static SalaryBreakdown CalculateSecondaryContract(UserSalary salary, double totalHours)
        {
            var validation = ValidateSecondaryContract(salary);
            if (validation != null || !salary.HasSecondaryContract)
                throw new ArgumentException(validation ?? "Druga umowa nie jest włączona.");
            var hours = (decimal)Math.Max(0, totalHours);
            var limit = (decimal)salary.SecondaryMonthlyHours!.Value;
            var secondaryHours = Math.Min(hours, limit);
            var primaryHours = hours - secondaryHours;
            var primaryRate = (decimal)salary.HourlyRate!.Value;
            var secondaryRate = (decimal)salary.SecondaryHourlyRate!.Value;
            var primaryAmount = Math.Round(primaryHours * primaryRate, 2, MidpointRounding.AwayFromZero);
            var secondaryAmount = Math.Round(secondaryHours * secondaryRate, 2, MidpointRounding.AwayFromZero);
            return new SalaryBreakdown
            {
                TotalHours = (double)hours, PrimaryHours = (double)primaryHours,
                PrimaryHourlyRate = (double)primaryRate, PrimaryAmount = (double)primaryAmount,
                SecondaryMonthlyHours = (double)limit, SecondaryHours = (double)secondaryHours,
                SecondaryHourlyRate = (double)secondaryRate, SecondaryAmount = (double)secondaryAmount,
                ExpectedAmount = (double)(primaryAmount + secondaryAmount)
            };
        }
    }
}
