using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public class PaymentWebhookPayload
{
    public string referenceCode { get; set; } = string.Empty;
    public decimal amountIn { get; set; }
    public string transactionContent { get; set; } = string.Empty;
}

public interface IPaymentService
{
    Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IMemberRepository _memberRepository;

    public PaymentService(IUnitOfWork unitOfWork, IPaymentRepository paymentRepository, IOrderRepository orderRepository, IMemberRepository memberRepository)
    {
        _unitOfWork = unitOfWork;
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _memberRepository = memberRepository;
    }

    public async Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload)
    {
        bool isDuplicate = await _paymentRepository.CheckTransactionExistsAsync(payload.referenceCode);
        if (isDuplicate)
            return true; 

        var regex = new System.Text.RegularExpressions.Regex(@"ORD\d{14}");
        var match = regex.Match(payload.transactionContent);
        
        if (!match.Success) return false;
        var extractedOrderCode = match.Value;

        var order = await _orderRepository.GetPendingOrderByCodeAsync(extractedOrderCode);
        if (order == null) return false;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var paymentTx = new PaymentTransaction
            {
                OrderId = order.OrderId,
                ReferenceCode = payload.referenceCode,
                Amount = payload.amountIn,
                Gateway = "SePay"
            };
            await _paymentRepository.AddTransactionAsync(paymentTx);

            if (payload.amountIn != order.TotalAmount)
            {
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return true; 
            }

            order.OrderStatus = OrderStatus.Paid;
            order.CompletedAt = DateTime.Now;

            foreach (var item in order.OrderDetails)
            {
                await _orderRepository.DecreaseStockAndReservedQuantityAsync(item.BookId, item.Quantity);
            }

            if (order.MemberId.HasValue)
            {
                var member = await _memberRepository.GetByIdAsync(order.MemberId.Value);
                if (member != null)
                {
                    if (order.PointsUsed > 0)
                    {
                        member.Points -= order.PointsUsed;
                        await _memberRepository.AddPointTransactionAsync(new PointTransaction
                        {
                            MemberId = member.MemberId,
                            Points = -order.PointsUsed,
                            Type = PointTransactionType.Redeemed
                        });
                    }
                    
                    int pointsEarned = (int)(order.TotalAmount / 10000);
                    if (pointsEarned > 0)
                    {
                        member.Points += pointsEarned;
                        await _memberRepository.AddPointTransactionAsync(new PointTransaction
                        {
                            MemberId = member.MemberId,
                            Points = pointsEarned,
                            Type = PointTransactionType.Earned
                        });
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
