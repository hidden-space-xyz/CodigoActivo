using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Messaging;
using CodigoActivo.Domain.Common;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common.Messaging;

public sealed class UnitOfWorkCommandDecoratorTests
{
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();

    private Task<TResult> HandleAsync<TResult>(TResult answer)
        where TResult : Result
    {
        var sut = new UnitOfWorkCommandDecorator<TestCommand<TResult>, TResult>(
            new AnsweringHandler<TResult>(answer),
            uow
        );
        return sut.HandleAsync(new TestCommand<TResult>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncHandlerSucceedsCommitsOnceAndReturnsItsResult()
    {
        var answer = Result.Success();

        var result = await HandleAsync(answer);

        result.Should().BeSameAs(answer);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncHandlerSucceedsWithAValueCommitsOnce()
    {
        var result = await HandleAsync(Result.Success(7));

        result.Value.Should().Be(7);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncHandlerFailsCommitsNothing()
    {
        Result answer = Error.Validation(ApplicationErrorCode.MessageInvalid);

        var result = await HandleAsync(answer);

        result.Should().BeSameAs(answer);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed record TestCommand<TResult> : ICommand<TResult>
        where TResult : Result;

    private sealed class AnsweringHandler<TResult>(TResult answer)
        : ICommandHandler<TestCommand<TResult>, TResult>
        where TResult : Result
    {
        public Task<TResult> HandleAsync(
            TestCommand<TResult> command,
            CancellationToken ct = default
        )
        {
            return Task.FromResult(answer);
        }
    }
}
