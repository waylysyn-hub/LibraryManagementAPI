using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Domain.Entities
{

    public class NotInFutureYearAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
        {
            if (value is int year)
            {
                var max = DateTime.UtcNow.Year; // السنة الحالية
                if (year < 1500 || year > max)
                    return new ValidationResult(ErrorMessage ?? $"Year must be between 1500 and {max}.");
            }
            return ValidationResult.Success;
        }
    }

}
