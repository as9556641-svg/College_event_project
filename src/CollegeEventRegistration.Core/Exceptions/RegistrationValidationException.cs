namespace CollegeEventRegistration.Core.Exceptions;

/// <summary>
/// Thrown when event registration validation rules fail (dates, status, eligibility, or unauthorized state transitions).
/// </summary>
public class RegistrationValidationException : InvalidOperationException
{
    public string RuleName { get; }

    public RegistrationValidationException(string ruleName, string message)
        : base(message)
    {
        RuleName = ruleName;
    }
}
