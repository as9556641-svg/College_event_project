namespace CollegeEventRegistration.Core.Exceptions;

/// <summary>
/// Thrown when an operation violates security policies protecting the root Super Admin account.
/// </summary>
public class SuperAdminProtectionException : InvalidOperationException
{
    public SuperAdminProtectionException(string message) : base(message) { }
    public SuperAdminProtectionException(string message, Exception innerException) : base(message, innerException) { }
}

