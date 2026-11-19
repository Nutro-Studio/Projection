# #14 4th Faction: Guilds Mechanics (Trading Posts, Caravans & Buyouts): Design Decisions

Branch: `feature/14-guild-mechanics` (from `week3`).
Sources: issue #14 (scope), Rulebook §3 and §5.3 (details).

## Decisions

| Topic | Decision | Source / reason |
|---|---|---|
| Trading Post | `ProvinceState.TradingPostOwnerId` holds at most one post per province. A post adds +1 to its Guild's hegemony strength, like a Church. | Issue + Rulebook §3. The hegemony point is our own choice: without it, a bought province goes straight back to the seller on re-evaluation. |
| Starting posts "in capitals" | Seed support only: `ProvinceSeed(..., tradingPostOwnerId)`, the same way the Guerrilla starting cell is seeded. No 4th player and no scene changes. | There is no capital concept and no Guild player on `week3`, and #17 is rebuilding the map and capitals, so that issue places the posts. |
| Trader per post | Every placed post (seeded or bought) comes with one Trader. | Rulebook §3 ("Grants +1 Trader") and §5.3 ("Gain 1 Trader per Trading Post"). Without it the Guild cannot run caravans: `BuyTrader` needs an owned province. |
| Caravan Network | `RunCaravan(origin, destination)`: a Trader standing on its own post travels the shortest path (BFS) to another of its own posts. It costs 1 AP, and the Trader moves there and counts as having acted. | Issue + Rulebook §5.3 ("travels between two Trading Posts"). |
| Caravan income | +2 Gold per province entered (the destination included, the origin not), plus +1 Gold per province for every 2 posts **built** beyond the starting ones. Posts from Hostile Buyout count as built. | Issue + Rulebook §5.3. |
| Transit fee | 1 Gold to the owner of each entered province that belongs to another player. Neutral and own provinces are free. | Issue + Rulebook §5.3. |
| Hostile Buyout | Target: another player's province with no Fortification, none of the owner's military units and no Trading Post. The Guild pays the market value to the owner, takes the province and places a Trading Post (with its Trader). Costs 1 AP. | Issue + Rulebook §5.3 ("unfortified", "instantly placing a Trading Post"). "No garrison" is our choice; otherwise the owner's troops would take the province back at once. |
| Market value | 5 Gold + 3 Gold per owner unit in the province (Fortification price + unit price). | Not defined in the issue or the Rulebook; our choice. |
| Guild scoring | At upkeep: +1 VP per Trading Post and +1 VP per 10 Gold banked (after revenue). Immediately: +2 VP for a route entering ≥ 3 provinces. | Issue + Rulebook §5.3. |
| Where scoring lives | `IFactionRuleset.CalculateUpkeepVictoryPoints`. The other factions keep +1 VP per province in this branch. | Architecture guide, no branching on `FactionType`. #18 replaces the other factions' scoring. |
| HUD | A Caravan button (then a click on a highlighted post), a Hostile Buyout (xG) button, and a "Trading Post (Px)" marker in the province panel. | No other issue covers UI for Guild actions. |

## Open questions

1. **Embargo veto** (Rulebook §5.3): the target can veto a buyout, which makes Guild caravans fair game to raid. It is not implemented, because hotseat needs the other player's answer in the middle of the Guild's turn and caravan raiding is a separate mechanic. Do we want it, and in which issue?
2. **Market value formula** (5G + 3G per unit) is a placeholder until balancing.
3. **Building Trading Posts for 4G** (Rulebook §3) is not in #14, so posts come only from seeds and Hostile Buyout.
4. **Trader cost** is 3G in the pre-existing code and 2G in the Rulebook. It is not changed here.
5. **War Financing / Mercenaries** (Rulebook §5.3) is not in any issue.
6. **Guild shift must lose all Trading Posts** (#15 matrix). To be added here when this branch is rebased onto merged #15.
7. **Who places the starting posts** (Militant and Zealot capitals) and adds the Guild player: #17 or a separate setup issue?
8. **Verification.** EditMode tests were run through NUnit against the Unity DLLs (`dotnet test`), not yet in the Unity Test Runner, and the HUD buttons have not been play-tested.
