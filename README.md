MiniBroker – Broker Service

MiniBroker is a lightweight, high-performance messaging broker built with gRPC and .NET, designed to enable structured, typed, event-driven communication between distributed services.
This repository contains the Broker Service, responsible for handling client connections, routing messages, dispatching typed handlers, and managing system-level notifications.

-------------------------------

⭐ Features

gRPC-based message transport
Fast, strongly typed communication using protobuf.

Typed message handling
Client libraries implement IHandleMessage<T> to process messages in a structured way.

Automatic handler discovery
(Optional) Supports source-generator–based message subscription generation to avoid reflection.

System notifications
Sends SystemNotificationMessage on client connect/disconnect (or other lifecycle events).

Minimal API integration
Simple server bootstrapping using

```
builder.Services.RegisterMiniBroker();
app.MapMiniBroker();
```


Extensible message model
Add new messages to your protobuf definitions and implement handlers on the client side.

-------------------------------

📦 Project Structure (wip)

```
/src
  /MiniBroker.Abstraction
  /MiniBroker.Client.SourceGen
  /MiniBroker.Client
  /MiniBroker.Grpc.Client
  /MiniBroker.Worker
  /proto
/tests
  /MiniBroker.Tests         # Unit & integration tests
/Demo
```
  
-------------------------------

  🚀 Getting Started (wip)

