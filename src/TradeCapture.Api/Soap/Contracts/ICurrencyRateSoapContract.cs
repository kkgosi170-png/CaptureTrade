namespace TradeCapture.Api.Soap.Contracts;

/// <summary>
/// SERVER-side SOAP contract, annotated with <c>CoreWCF</c> attributes. CoreWCF hosts the service
/// against this interface. It is kept wire-compatible with the client contract
/// (<see cref="ICurrencyRateSoapClient"/>) by sharing the same Name, Namespace and SOAP Action.
/// </summary>
[CoreWCF.ServiceContract(Name = CurrencyRateContract.ServiceName, Namespace = CurrencyRateContract.Namespace)]
public interface ICurrencyRateSoapContract
{
    /// <summary>Returns the latest rate effective on/before <paramref name="asOf"/>, or null if none.</summary>
    [CoreWCF.OperationContract(
        Action = CurrencyRateContract.GetRateAction,
        ReplyAction = CurrencyRateContract.GetRateReplyAction)]
    CurrencyRateDto? GetRate(string fromCurrency, string toCurrency, DateTime asOf);
}
