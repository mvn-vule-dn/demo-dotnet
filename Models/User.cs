using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace demo.Models;

public partial class User
{
    public int Id { get; set; }

    [EmailAddress(ErrorMessage = "email is invalid")]
    [Required(ErrorMessage = "email is missing")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "fullname is missing")]
    public string? Fullname { get; set; }

    [Required(ErrorMessage = "username is missing")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "password is missing")]
    [StringLength(255, ErrorMessage = "password must be more than 8 characters", MinimumLength = 8)]
    public string? Password { get; set; }

    // [Required(ErrorMessage = "birthday is missing")]
    public DateOnly? Birthday { get; set; }

    public int? Status { get; set; }
    public string? Code { get; set; }
}
