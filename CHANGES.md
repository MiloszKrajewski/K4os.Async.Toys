## 0.0.18 (2023/11/17)
* ADDED: Configurable PollerCount in BatchBuilder

## 0.0.17 (2023/11/10)
* FIXED: double dispose on BatchBuilder was throwing an exception

## 0.0.16 (2023/11/07)
* ADDED: exposed previously internal DisposableBag

## 0.0.15 (2023/11/06)
* FIXED: even more bugs in AliveKeeper
* ADDED: BatchSubscriber allows to subscribe to any poll-able source

## 0.0.14 (2023/11/02)
* FIXED: multiple bugs in AliveKeeper

## 0.0.13 (2023/05/18)
* ADDED: maximum concurrency setting
* ADDED: concurrency policies for AliveKeeper
* FIXED: safe sync policy was deadlocking in some cases
* ADDED: ability for agents to end their work without being disposed
* ADDED: cancellation token can be passed to agents

## 0.0.8 (2023/05/06)
* CHANGED: build process
* ADDED: GitHub actions
* FIXED: Minor bug in AliveKeeper (sometimes actions were called with not items at all)
* CHANGED: Minor optimization in AliveKeeper (removed unnecessary allocations)
* ADDED: Explicit target for .NET 5

## 0.0.5 (2021/04/11)
* ADDED: ITimeSource to ManualResetSignal

* ## 0.0.4 (2021/04/10)
* ADDED: ManualResetSignal

## 0.0.3 (2021/04/02)
* ADDED: AliveKeeper
* ADDED: BatchBuilder
