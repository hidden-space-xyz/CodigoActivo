using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Tells the outbox deliverer whose fault a failed send was, which decides what happens next.
/// </summary>
public enum EmailDeliveryFailure
{
    /// <summary>
    /// The server refused the recipient for good, such as a mailbox that does not exist; the message
    /// is dropped.
    /// </summary>
    RecipientRejected,

    /// <summary>
    /// The message can never be sent as it is, such as one larger than the server accepts or one
    /// whose stored content cannot be read; it is dropped.
    /// </summary>
    MessageRejected,

    /// <summary>
    /// The server asked to try this recipient again later, such as with greylisting; the message is
    /// retried later and the rest of the delivery goes on.
    /// </summary>
    RecipientDeferred,

    /// <summary>
    /// The send failed for a reason that may pass and is not known to be the recipient's; the message
    /// is retried later, and several in a row are blamed on the server.
    /// </summary>
    Transient,

    /// <summary>
    /// The server cannot take mail now: it is unreachable, refused the login or the sender, or asked
    /// to slow down. The whole delivery pauses without spending any attempt.
    /// </summary>
    ServerUnavailable,
}

/// <summary>
/// Represents a send the transport could not complete, with whose fault it was. Its message never
/// quotes the reply of the server, which may name the recipient.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    /// <summary>
    /// Initializes a transient failure without a message.
    /// </summary>
    public EmailDeliveryException()
        : this(EmailDeliveryFailure.Transient, "The email could not be delivered.", null) { }

    /// <summary>
    /// Initializes a transient failure with a diagnostic message.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    public EmailDeliveryException(string message)
        : this(EmailDeliveryFailure.Transient, message, null) { }

    /// <summary>
    /// Initializes a transient failure with a diagnostic message and its cause.
    /// </summary>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    /// <param name="innerException">Underlying exception that caused the failure.</param>
    public EmailDeliveryException(string message, Exception innerException)
        : this(EmailDeliveryFailure.Transient, message, innerException) { }

    /// <summary>
    /// Initializes a failure whose fault is known.
    /// </summary>
    /// <param name="failure">Whose fault the failure was.</param>
    /// <param name="message">Diagnostic message; it must not contain personal data.</param>
    /// <param name="innerException">
    /// Underlying exception, or <see langword="null"/> when it could quote the server reply.
    /// </param>
    public EmailDeliveryException(
        EmailDeliveryFailure failure,
        string message,
        Exception? innerException
    )
        : base(message, innerException)
    {
        Failure = failure;
    }

    /// <summary>
    /// Gets whose fault the failure was.
    /// </summary>
    public EmailDeliveryFailure Failure { get; }
}

/// <summary>
/// Opens connections to the mail server. The outbox deliverer is its only caller: it opens one
/// session per run and hands it every message the run sends.
/// </summary>
public interface IEmailTransport
{
    /// <summary>
    /// Opens a session with the mail server, connected and authenticated.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the open session, which the caller disposes.</returns>
    /// <exception cref="EmailDeliveryException">
    /// The server cannot be reached or refused the login, with
    /// <see cref="EmailDeliveryFailure.ServerUnavailable"/>.
    /// </exception>
    public Task<IEmailTransportSession> OpenAsync(CancellationToken ct = default);
}

/// <summary>
/// Hands addressed email to the mail server over one connection, with no quota, queue or retry of
/// its own; a connection lost between two messages is opened again before the next.
/// </summary>
public interface IEmailTransportSession : IAsyncDisposable
{
    /// <summary>
    /// Delivers the message right away; a failure leaves as an exception.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EmailDeliveryException">The send failed, with whose fault it was.</exception>
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
