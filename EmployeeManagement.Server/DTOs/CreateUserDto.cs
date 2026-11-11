using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.DTOs
{
    // DTO percakton te dhenat qe administratori dergon per nje perdorues te ri.
    // Nuk eshte tabele databaze dhe nuk perdoret si pergjigje: password-i mbetet vetem ne kerkese.
    public class CreateUserDto
    {
        // Annotations kontrollohen nga [ApiController]; fushat e pavlefshme japin automatikisht 400.
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        // Identity kontrollon edhe shkronjen e madhe, te voglen, numrin dhe simbolin.
        // MinLength pasqyron minimumin aktual prej 6 karakteresh; UserManager zbaton rregullat e plota.
        [Required]
        [MinLength(6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        // Lista e mbyllur nuk lejon krijimin ose caktimin e roleve te tjera nga klienti.
        [Required]
        [RegularExpression("^(Employee|Administrator)$")]
        public string Role { get; set; } = string.Empty;
    }
}
