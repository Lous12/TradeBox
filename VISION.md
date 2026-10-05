# Original vision

This document records what I wanted **TradeBox** to become. It is not a promise or an active release schedule.

## Core idea

Expand the economy that already exists in WorldBox instead of replacing it with an unrelated parallel simulation.

Cities should have understandable economic conditions: resources, reserves, shortages, surpluses, prosperity and trade. The systems should be visible in the UI and should eventually create things the player can actually see in the world.

## Principles

- use real vanilla resources whenever possible;
- keep TradeBox standalone;
- avoid turning the game into an Excel sheet;
- make economic state readable at a glance;
- prefer visible consequences and world activity over hidden numbers;
- keep Political World integration in a separate addon rather than coupling both cores together.

## Systems I wanted to build

### City economy

- reserves and target reserves;
- shortages and surpluses;
- trends over time;
- prosperity / economic health;
- markets and economic buildings where useful.

### Trade

- internal trade between cities;
- later, trade between neighboring states/countries;
- international trade routes;
- visible caravans or other world actors representing trade instead of only changing numbers invisibly.

### Larger economic systems

- monopolies;
- different economic systems / policy models;
- government economic offices or tax collectors;
- richer production/consumption pressure built around vanilla resources.

### Political World integration

A separate compatibility addon could connect trade/economy to Political World after TradeBox has a stable international-trade layer. The core TradeBox mod should still work without Political World.

## What I did not want

- fake resources that ignore the vanilla economy for no reason;
- a hard dependency on Political World;
- a giant economy UI full of numbers with no gameplay meaning;
- invisible trade forever if it can eventually be represented in the world.
