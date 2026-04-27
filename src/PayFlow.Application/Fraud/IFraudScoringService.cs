using System.Threading;
using System.Threading.Tasks;

namespace PayFlow.Application.Fraud;

public interface IFraudScoringService
{
    Task<double> GetFraudScoreAsync(PaymentTransactionData transaction, CancellationToken ct = default);
}