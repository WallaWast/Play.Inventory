using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Play.Common;
using Play.Inventory.Contracts;
using Play.Inventory.Service.Entities;
using Play.Inventory.Service.Exceptions;

namespace Play.Inventory.Service.Consumers;

public class GrantItemsConsumer : IConsumer<GrantItems>
{
    private readonly IRepository<InventoryItem> _inventoryitemsRepository;
    private readonly IRepository<CatalogItem> _catalogItemsRepository;
    private readonly ILogger<GrantItemsConsumer> _logger;

    public GrantItemsConsumer(IRepository<InventoryItem> inventoryitemsRepository, IRepository<CatalogItem> catalogItemsRepository, ILogger<GrantItemsConsumer> logger)
    {
        _inventoryitemsRepository = inventoryitemsRepository;
        _catalogItemsRepository = catalogItemsRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GrantItems> context)
    {
        var message = context.Message;

        _logger.LogInformation("Received the request to grant the item {CatalogItemId} with the quantity of {Quantity} to the user {UserId} with the correlationId {CorrelationId}",
        message.CatalogItemId,
        message.Quantity,
        message.UserId,
        message.CorrelationId);

        var item = await _catalogItemsRepository.GetAsync(message.CatalogItemId);

        if (item is null)
        {
            throw new UnknownItemException(message.CatalogItemId);
        }

        var inventoryItem = await _inventoryitemsRepository
                                    .GetAsync(item =>
                                        item.UserId == message.UserId &&
                                        item.CatalogItemId == message.CatalogItemId);

        if (inventoryItem == null)
        {
            inventoryItem = new InventoryItem
            {
                CatalogItemId = message.CatalogItemId,
                UserId = message.UserId,
                Quantity = message.Quantity,
                AcquiredDate = DateTimeOffset.UtcNow
            };

            // The ID on the header of the message, case it is a retry will have the same
            inventoryItem.MessageIds.Add(context.MessageId.Value);

            await _inventoryitemsRepository.CreateAsync(inventoryItem);
        }
        else
        {
            if (inventoryItem.MessageIds.Contains(context.MessageId.Value))
            {
                await context.Publish(new InventoryItemsGranted(message.CorrelationId));
                return;
            }

            inventoryItem.Quantity += message.Quantity;
            // The ID on the header of the message, case it is a retry will have the same
            inventoryItem.MessageIds.Add(context.MessageId.Value);

            await _inventoryitemsRepository.UpdateAsync(inventoryItem);
        }

        var itemsGrantedTask = context.Publish(new InventoryItemsGranted(message.CorrelationId));
        var inventoryUpdatedTask = context.Publish(new InventoryItemUpdated(inventoryItem.UserId, inventoryItem.CatalogItemId, inventoryItem.Quantity));

        await Task.WhenAll(itemsGrantedTask, inventoryUpdatedTask);
    }
}