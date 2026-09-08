using System.ComponentModel.DataAnnotations;

namespace StudyMate.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required, StringLength(80, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string Name { get; set; }

        [Required, EmailAddress, StringLength(160)]
        public string Email { get; set; }

        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; }

        [Required, StringLength(80)]
        public string Major { get; set; }

        [Range(1, 5)]
        public int Year { get; set; } = 1;
    }
}
