using System.ComponentModel.DataAnnotations;

namespace EmployeeManagement.Server.DTOs
{
    // PUT kerkon te gjitha fushat e profilit; password-i nuk ndryshohet nga ky veprim.
    public class UpdateUserDto
    {
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

        [Required]
        [RegularExpression("^(Employee|Administrator)$")]
        public string Role { get; set; } = string.Empty;
    }
}
