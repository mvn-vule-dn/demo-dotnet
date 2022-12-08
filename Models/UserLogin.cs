using System.ComponentModel.DataAnnotations;

namespace demo.Models;

public  class UserLogin
    {
        [Required(ErrorMessage = "username is missing")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "password is missing")]
        public string? Password { get; set; }
    }