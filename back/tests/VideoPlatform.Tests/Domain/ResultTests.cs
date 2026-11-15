// File: ResultTests.cs
// Purpose: Verifies the Result/Result<T> pattern's own invariants — the
// mechanism every Application handler uses to report expected failures
// (Domain/Common/Result.cs).
namespace VideoPlatform.Tests.Domain;

using VideoPlatform.Domain.Common;
using Xunit;

public class ResultTests
{
    [Fact]
    public void Success_result_is_success_and_carries_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Failure_result_carries_the_given_error_message()
    {
        var result = Result.Failure("something went wrong");

        Assert.True(result.IsFailure);
        Assert.Equal("something went wrong", result.Error);
    }

    [Fact]
    public void Failure_with_an_empty_error_message_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(string.Empty));
    }

    [Fact]
    public void Generic_success_result_exposes_its_value()
    {
        var result = Result.Success(42);

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Reading_the_value_of_a_failed_generic_result_throws()
    {
        var result = Result.Failure<int>("nope");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_a_value_produces_a_success_result()
    {
        Result<int> result = 7;

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }
}
