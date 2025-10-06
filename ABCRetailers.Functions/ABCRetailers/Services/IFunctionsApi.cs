using ABCRetailers.Models;

namespace ABCRetailers.Services;

public interface IFunctionsApi
{
    Task<List<Customer>> GetCustomersAsync();
    Task<Customer?> GetCustomerAsync(string id);
    Task<Customer> CreateCustomerAsync(Customer c);
    Task<Customer> UpdateCustomerAsync(string id, Customer c);
    Task DeleteCustomerAsync(string id);

    Task<List<Product>> GetProductsAsync();
    Task<Product?> GetProductAsync(string id);
    Task<Product> CreateProductAsync(Product p, IFormFile? imageFile);
    Task<Product> UpdateProductAsync(string id, Product p, IFormFile? imageFile);
    Task DeleteProductAsync(string id);

    Task<List<Order>> GetOrdersAsync();
    Task<Order?> GetOrderAsync(string id);
    Task<Order> CreateOrderAsync(string customerId, string productId, int quantity);
    Task UpdateOrderStatusAsync(string id, string newStatus);
    Task DeleteOrderAsync(string id);

    Task<string> UploadProofOfPaymentAsync(IFormFile file, string? orderId, string? customerName);
}
