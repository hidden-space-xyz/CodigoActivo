using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.TermsDecisions.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Infrastructure.Legal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Legal;

public sealed class LegalNoticeWorkerTests
{
    [Fact]
    public async Task NotifyAsyncFailedWithdrawalNoticeStillSendsTheConsultationNotice()
    {
        var withdrawals = Substitute.For<
            ICommandHandler<SendConsentWithdrawalNoticesCommand, Result<int>>
        >();
        withdrawals
            .HandleAsync(
                Arg.Any<SendConsentWithdrawalNoticesCommand>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromException<Result<int>>(
                    new InvalidOperationException("The notice is larger than the server accepts.")
                )
            );
        var consultations = Substitute.For<
            ICommandHandler<SendBlockedDataConsultationNoticesCommand, Result<int>>
        >();
        consultations
            .HandleAsync(
                Arg.Any<SendBlockedDataConsultationNoticesCommand>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult<Result<int>>(1));
        await using var provider = new ServiceCollection()
            .AddSingleton(withdrawals)
            .AddSingleton(consultations)
            .BuildServiceProvider();
        var worker = new LegalNoticeWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new LegalNoticeOptions(),
            NullLogger<LegalNoticeWorker>.Instance
        );

        var act = () => worker.NotifyAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await consultations
            .Received(1)
            .HandleAsync(
                Arg.Any<SendBlockedDataConsultationNoticesCommand>(),
                Arg.Any<CancellationToken>()
            );
    }
}
