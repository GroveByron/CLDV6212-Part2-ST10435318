using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ABCRetailers.Functions.Entities;

namespace ABCRetailers.Functions.Functions;

public class QueueProcessorFunctions
{
    private readonly IConfiguration _config;

    public QueueProcessorFunctions(IConfiguration config)
    {
        _config = config;
    }

    [Function("OrderNotifications_Processor")]
    public async Task OrderNotificationsProcessor(
        [QueueTrigger("%QUEUE_ORDER_NOTIFICATIONS%", Connection = "STORAGE_CONNECTION")] string message,
        FunctionContext ctx)
    {
        var log = ctx.GetLogger("OrderNotifications_Processor");

        if (string.IsNullOrWhiteSpace(message))
        {
            log.LogError("Received empty message from order-notifications queue. Skipping.");
            return;
        }

        log.LogInformation($"Processing order notification: {message}");

        try
        {
            var orderData = JsonSerializer.Deserialize<OrderMessage>(message);

            if (orderData == null)
            {
                log.LogError("Failed to deserialize order message: {Message}", message);
                return;
            }

            switch (orderData.Type)
            {
                case "OrderCreated":
                    var conn = _config["STORAGE_CONNECTION"] ?? throw new InvalidOperationException("STORAGE_CONNECTION missing");
                    var ordersTable = _config["TABLE_ORDER"] ?? "Order";

                    var table = new TableClient(conn, ordersTable);
                    await table.CreateIfNotExistsAsync();

                    var order = new OrderEntity
                    {
                        RowKey = orderData.OrderId,
                        CustomerId = orderData.CustomerId,
                        ProductId = orderData.ProductId,
                        ProductName = orderData.ProductName,
                        Quantity = orderData.Quantity,
                        UnitPrice = orderData.UnitPrice,
                        OrderDateUtc = orderData.OrderDateUtc,
                        Status = orderData.Status
                    };

                    await table.AddEntityAsync(order);
                    log.LogInformation($"Order {order.RowKey} successfully written to Orders table via queue trigger");
                    break;

                case "OrderStatusUpdated":
                    log.LogInformation($"Order status updated: {orderData.OrderId} -> {orderData.NewStatus}");
                    break;

                default:
                    log.LogWarning($"Unknown order message type: {orderData.Type}");
                    break;
            }
        }
        catch (JsonException jex)
        {
            log.LogError(jex, "Invalid JSON in message: {Message}", message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error processing order notification");
        }
    }

    [Function("StockUpdates_Processor")]
    public void StockUpdatesProcessor(
        [QueueTrigger("%QUEUE_STOCK_UPDATES%", Connection = "STORAGE_CONNECTION")] string message,
        FunctionContext ctx)
    {
        var log = ctx.GetLogger("StockUpdates_Processor");

        if (string.IsNullOrWhiteSpace(message))
        {
            log.LogError("Received empty message from stock-updates queue. Skipping.");
            return;
        }

        log.LogInformation($"Processing stock update message: {message}");

        try
        {
            var stockData = JsonSerializer.Deserialize<StockMessage>(message);
            if (stockData != null)
            {
                log.LogInformation($"Stock updated for product {stockData.ProductName}: {stockData.PreviousStock} -> {stockData.NewStock}");
            }
            else
            {
                log.LogError("Failed to deserialize stock message: {Message}", message);
            }
        }
        catch (JsonException jex)
        {
            log.LogError(jex, "Invalid JSON in stock message: {Message}", message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error processing stock update");
        }
    }

    // Messages expected from queue
    private record OrderMessage(
        string Type,
        string OrderId,
        string CustomerId,
        string? CustomerName,
        string ProductId,
        string ProductName,
        int Quantity,
        double UnitPrice,
        double TotalAmount,
        DateTimeOffset OrderDateUtc,
        string Status,
        string? PreviousStatus = null,
        string? NewStatus = null
    );

    private record StockMessage(
        string Type,
        string ProductId,
        string ProductName,
        int PreviousStock,
        int NewStock,
        DateTimeOffset UpdatedDateUtc,
        string UpdatedBy
    );
}