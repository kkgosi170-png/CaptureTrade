using System.Runtime.Serialization;

namespace TradeCapture.Api.Soap.Contracts;

/// <summary>Shared SOAP contract constants so the server and client contracts stay wire-compatible.</summary>
public static class CurrencyRateContract
{
    public const string Namespace = "http://trackerconnect/currency";
    public const string ServiceName = "CurrencyRateService";
    public const string GetRateAction = Namespace + "/GetRate";
    public const string GetRateReplyAction = Namespace + "/GetRateResponse";
}

/// <summary>
/// SOAP data contract for a currency rate. Uses System.Runtime.Serialization attributes, which are
/// common to both CoreWCF (server) and System.ServiceModel (client), so this type is shared by both.
/// </summary>
[DataContract(Namespace = CurrencyRateContract.Namespace)]
public class CurrencyRateDto
{
    [DataMember] public string From { get; set; } = "";
    [DataMember] public string To { get; set; } = "";
    [DataMember] public decimal Rate { get; set; }
    [DataMember] public DateTime AsOf { get; set; }
}
