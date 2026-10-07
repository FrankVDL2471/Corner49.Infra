# Corner49.Infra.AzureServiceBus

Opinionated messaging on Azure Service Bus. Part of Corner49.Infra – a set of packages that provide opinionated tooling for infrastructure in .NET applications.

- Queues, topics and subscriptions are created on first use
- Typed messages with a sender service and a handler whose methods are picked by action name
- Handlers run as hosted services, with a fresh dependency injection scope per message
- A developer mode that lets several developers share one namespace without taking each other's messages

```
dotnet add package Corner49.Infra.AzureServiceBus
```

Already included when you install [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra).

## Configuration

```json
{
  "ServiceBus": {
    "ConnectString": "Endpoint=sb://...;SharedAccessKeyName=...;SharedAccessKey=...",
    "DeveloperMode": false,
    "MaxDeliveryCount": 10,
    "IsBasicTier": false
  }
}
```

| Key | Default | Meaning |
|---|---|---|
| `ConnectString` | – | Namespace connection string. It needs manage rights, because entities are created on demand. |
| `DeveloperMode` | `false` | Isolates messages per machine, see below. |
| `MaxDeliveryCount` | `10` | Delivery attempts before a message is dead-lettered. |
| `IsBasicTier` | `false` | Set to `true` on a Basic tier namespace; disables duplicate detection on queues. |

## Getting started

Register the service bus and its handlers on an infra builder (Corner49.Infra or Corner49.Infra.CLI):

```csharp
infra.AddServiceBus(
    cfg => { /* override ServiceBusConfiguration here if needed */ },
    bus => {
        bus.AddMessageService<OrderMessage, OrderMessageService>();
        bus.AddMessageHandler<OrderMessage, OrderMessageHandler>();
    });
```

This registers `IServiceBusService` as a singleton and one hosted listener per handler.

## Typed messages

The recommended way to publish and consume. A message class names its topic (or queue), a service sends it, and a handler receives it.

```csharp
using Corner49.Infra.Messages;

public class OrderMessage : MessageBase {
    public OrderMessage() : base("orders") { }          // topic name; pass useQueue: true for a queue
    public Order? Order { get; set; }

    public override string GetMessageId() => Order?.Id; // optional, enables duplicate detection
}
```

**Sending**

```csharp
public class OrderMessageService : MessageService<OrderMessage> {
    public OrderMessageService(ILogger<OrderMessageService> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider) { }

    public Task Created(Order order) =>
        Send(new OrderMessage { Action = nameof(Created), Order = order });
}
```

`Action` is required. `Send(msg, enqueueTime)` schedules a message, `Send(msg, throttleDelay)` collapses bursts of messages with the same message id within one process, and `SendBulk` sends a batch.

**Receiving**

```csharp
public class OrderMessageHandler : MessageHandler<OrderMessage> {
    private readonly IOrderRepo _repo;

    public OrderMessageHandler(IOrderRepo repo) {
        _repo = repo;
    }

    // called when Action == "Created"
    public Task Created(OrderMessage msg) => _repo.Save(msg.Order!);
}
```

The handler method is found by name: it must be public, be named exactly like the sender's `Action`, and take the message type as its only parameter. Override `Process(action, message)` to see every message regardless of action.

`AddMessageHandler<T, H>(maxConcurrentCalls?)` listens on the message's queue, or on a topic subscription named `{appName}.{MessageTypeName}`. It processes 10 messages concurrently by default.

## Low-level API

For interop with other systems, or when you need full control over the entity.

**Sending** – inject `IServiceBusService`:

```csharp
using Corner49.Infra.ServiceBus;

var sender = serviceBus.GetTopicSender("orders");     // or GetQueueSender("orders")

var cmd = new ServiceBusCommand { Name = "Created", Source = "Shop" };
cmd.SetData(order);

await sender.Send(cmd);
long sequence = await sender.Send(cmd, DateTimeOffset.UtcNow.AddMinutes(5));   // scheduled
await sender.Cancel(sequence);                                                 // cancel a scheduled message
```

**Receiving** – implement `IServiceBusHandler`:

```csharp
public class OrderHandler : IServiceBusHandler {
    public Task MessageReceived(ServiceBusCommand msg) {
        var order = msg.GetData<Order>();
        return Task.CompletedTask;
    }
}

infra.AddServiceBus(cfg => { }, bus => {
    bus.AddServiceBusHandler<OrderHandler>(opt => {
        opt.Name = "orders";
        opt.Kind = ServiceBusKind.Topic;          // default is Queue
        opt.SubscriptionName = "shop";
        opt.SubscriptionFilter = "Target = 'Shop'";
        opt.MaxConcurrentCalls = 30;
    });
});
```

| Option | Default | Meaning |
|---|---|---|
| `Name` | – | Queue or topic name (required) |
| `Kind` | `Queue` | `Queue` or `Topic` |
| `SubscriptionName` | handler type name | Subscription to use for a topic |
| `SubscriptionFilter` | none | SQL filter for the subscription |
| `MaxConcurrentCalls` | `30` | Messages processed in parallel |
| `PrefetchCount` | `0` | Messages fetched ahead into a local cache |
| `DuplicateDetectionWindow` | none | Enables duplicate detection when the entity is created |
| `TrackMessageCount` | `false` | Fills `ServiceBusCommand.MessageCount`; costs a management call per message |
| `ConnectString` | – | Use another namespace for this handler |

`IServiceBusService` also offers `GetMessageCount`, `ResubmitDeadletterQueue`, `DeleteQueue` and `IsTopicFull`.

## Behaviour to be aware of

- **Names are lower-cased.** `Orders` and `orders` are the same entity.
- **Queue handlers receive in `ReceiveAndDelete` mode.** A message is removed from the queue when it is handed to your handler, so a failing handler does not get it again. Handle errors inside the handler when the work must not be lost.
- **Handler exceptions are logged, not rethrown.** A failing message never stops the listener.
- **Changing a `SubscriptionFilter` recreates the subscription**, which drops the messages waiting in it.
- **Created entities get defaults**: queues and subscriptions use a 5 minute lock and dead-letter expired messages; messages live for 7 days; topics are 5 GB and are removed after 30 days idle.

## Developer mode

Set `ServiceBus:DeveloperMode` to `true` on a developer machine that shares a namespace with others:

- Each machine gets its own queue, named `{queue}.{machinename}`.
- Each machine gets its own topic subscription, filtered on `Target = '{MachineName}'`, and outgoing messages without a `Target` are stamped with the machine name.
- These personal entities are cleaned up automatically when idle.

The result: you only receive the messages your own process sent. Leave it off in shared environments.

## Related packages

- [Corner49.Infra](https://www.nuget.org/packages/Corner49.Infra) – web apps and APIs
- [Corner49.Infra.CLI](https://www.nuget.org/packages/Corner49.Infra.CLI) – console apps and jobs
- [Corner49.Infra.Core](https://www.nuget.org/packages/Corner49.Infra.Core) – abstractions and utilities

Source: https://github.com/FrankVDL2471/Corner49.Infra
