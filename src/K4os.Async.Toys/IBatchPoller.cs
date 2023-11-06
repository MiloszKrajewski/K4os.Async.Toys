namespace K4os.Async.Toys;

/// <summary>
/// Abstraction of polling. Assumed existence of three operations: <see cref="Receive"/> gets
/// multiple items from underlying data source, <see cref="Delete"/> marks items as processed,
/// and <see cref="Touch"/> extends lifespan of items (making the as "still being processed").
/// </summary>
/// <typeparam name="TMessage">Type of message.</typeparam>
/// <typeparam name="TReceipt">Type of message receipt.</typeparam>
public interface IBatchPoller<TMessage, TReceipt>
	where TMessage: notnull
	where TReceipt: notnull
{
	/// <summary>Gets message receipt.</summary>
	/// <param name="message">Message.</param>
	/// <returns>Receipt.</returns>
	TReceipt ReceiptFor(TMessage message);
	
	/// <summary>
	/// Gets identity of the receipt. This is used as a key in dictionary to track state of
	/// given message.
	/// </summary>
	/// <param name="receipt">Receipt.</param>
	/// <returns>A receipt's key.</returns>
	string IdentityOf(TReceipt receipt);

	/// <summary>Receives multiple messages from underlying data source.</summary>
	/// <param name="token">Cancellation token.</param>
	/// <returns>A batch of messages.</returns>
	Task<TMessage[]> Receive(CancellationToken token);
	
	/// <summary>Deletes / marks multiple messages as completed. This is called when message was successfully processed.</summary>
	/// <param name="receipts">Batch of message receipts.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>List of successful updates.</returns>
	Task<TReceipt[]> Delete(TReceipt[] receipts, CancellationToken token);
	
	/// <summary>Extends lifespan of multiple messages. This is called periodically while message is still being processed.</summary>
	/// <param name="receipts">Batch of message receipts.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>List of successful updates.</returns>
	Task<TReceipt[]> Touch(TReceipt[] receipts, CancellationToken token);
}
