using System.ComponentModel.DataAnnotations;

namespace CollegeEventRegistration.Web.Areas.Admin.Models;

public class CategoryIndexViewModel
{
    public IReadOnlyList<CategoryItemViewModel> Categories { get; set; } = new List<CategoryItemViewModel>();
    public string? SearchTerm { get; set; }
}

public class CategoryItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconClass { get; set; }
    public bool IsActive { get; set; }
    public int EventCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CategoryCreateViewModel
{
    [Required(ErrorMessage = "Category Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Category name must be between 2 and 100 characters.")]
    [Display(Name = "Category Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "Icon class cannot exceed 50 characters.")]
    [Display(Name = "Icon (e.g. bi-laptop, bi-trophy, bi-palette, bi-award)")]
    public string? IconClass { get; set; }
}

public class CategoryEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Category name must be between 2 and 100 characters.")]
    [Display(Name = "Category Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Slug")]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "Icon class cannot exceed 50 characters.")]
    [Display(Name = "Icon")]
    public string? IconClass { get; set; }

    [Display(Name = "Active Status")]
    public bool IsActive { get; set; }
}

