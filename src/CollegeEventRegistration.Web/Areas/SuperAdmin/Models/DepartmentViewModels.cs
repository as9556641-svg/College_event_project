using System.ComponentModel.DataAnnotations;
using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Web.Areas.SuperAdmin.Models;

public class DepartmentIndexViewModel
{
    public IReadOnlyList<DepartmentItemViewModel> Departments { get; set; } = new List<DepartmentItemViewModel>();
    public string? SearchTerm { get; set; }
}

public class DepartmentItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? HeadOfDepartment { get; set; }
    public bool IsActive { get; set; }
    public int EventCount { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DepartmentCreateViewModel
{
    [Required(ErrorMessage = "Department Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Department name must be between 2 and 100 characters.")]
    [Display(Name = "Department Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department Code is required.")]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Code must be between 2 and 10 characters.")]
    [RegularExpression(@"^[A-Z0-9\-]+$", ErrorMessage = "Code must contain only uppercase letters, numbers, and hyphens.")]
    [Display(Name = "Department Code (e.g. CSE, ECE)")]
    public string Code { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(100, ErrorMessage = "Head of Department cannot exceed 100 characters.")]
    [Display(Name = "Head of Department")]
    public string? HeadOfDepartment { get; set; }
}

public class DepartmentEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Department Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Department name must be between 2 and 100 characters.")]
    [Display(Name = "Department Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department Code is required.")]
    [StringLength(10, MinimumLength = 2, ErrorMessage = "Code must be between 2 and 10 characters.")]
    [RegularExpression(@"^[A-Z0-9\-]+$", ErrorMessage = "Code must contain only uppercase letters, numbers, and hyphens.")]
    [Display(Name = "Department Code")]
    public string Code { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(100, ErrorMessage = "Head of Department cannot exceed 100 characters.")]
    [Display(Name = "Head of Department")]
    public string? HeadOfDepartment { get; set; }

    [Display(Name = "Active Status")]
    public bool IsActive { get; set; }
}

public class DepartmentDetailsViewModel
{
    public Department Department { get; set; } = null!;
    public int EventCount { get; set; }
    public int UserCount { get; set; }
    public List<Event> AssociatedEvents { get; set; } = new();
    public bool CanDelete { get; set; }
    public string? DeleteBlockReason { get; set; }
}

