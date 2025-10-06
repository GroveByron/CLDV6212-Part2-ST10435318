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
        log.LogInformation($"Processing order notification: {message}");

        try
        {
            // Deserialize the message
            var orderData = JsonSerializer.Deserialize<OrderMessage>(message);

            if (orderData == null)
            {
                log.LogError("Failed to deserialize order message");
                return;
            }

            // Only process OrderCreated messages (write to table)
            if (orderData.Type == "OrderCreated")
            {
                var conn = _config["STORAGE_CONNECTION"] ?? throw new InvalidOperationException("STORAGE_CONNECTION missing");
                var ordersTable = _config["TABLE_ORDER"] ?? "Order";

                var table = new TableClient(conn, ordersTable);
                await table.CreateIfNotExistsAsync();

                // Create the order entity
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

                // Write to Orders table via queue trigger
                await table.AddEntityAsync(order);
                log.LogInformation($"Order {order.RowKey} successfully written to Orders table via queue trigger");
            }
            else if (orderData.Type == "OrderStatusUpdated")
            {
                log.LogInformation($"Order status updated: {orderData.OrderId} -> {orderData.NewStatus}");
                // Optional: Send email notification, update external systems, etc.
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error processing order notification");
            throw; // Re-throw to trigger poison queue handling
        }
    }

    [Function("StockUpdates_Processor")]
    public void StockUpdatesProcessor(
        [QueueTrigger("%QUEUE_STOCK_UPDATES%", Connection = "STORAGE_CONNECTION")] string message,
        FunctionContext ctx)
    {
        var log = ctx.GetLogger("StockUpdates_Processor");
        log.LogInformation($"StockUpdates message: {message}");

        // Optional: Sync to reporting DB, send inventory alerts, etc.
        try
        {
            var stockData = JsonSerializer.Deserialize<StockMessage>(message);
            if (stockData != null)
            {
                log.LogInformation($"Stock updated for product {stockData.ProductName}: {stockData.PreviousStock} -> {stockData.NewStock}");
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error processing stock update");
        }
    }

    // Message DTOs for deserialization
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