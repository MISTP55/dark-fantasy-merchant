# Social classes and satisfaction — design

## Intent

Patrician III splits a city's inhabitants into four classes: rich, wealthy, poor and beggars. Dark Fantasy Merchant has only two groups so far: the housed, who are P3's poor, and the beggars. This work adds the wealthy and the rich, their houses and P3's satisfaction system, which decides how many of the poor rise into those classes and how many leave the city.

For now the player only sees one counter per class. Each class will bring its own benefits later. The aim is a population that behaves as P3's does, so that those benefits can be built on it.

Every rule comes from `patrician3-re\resultats\Population des villes.md` and `Production et consommation des villes.md`. Where this document gives a figure, the figure is P3's. Where it says "as P3", the builder reads the formula in those notes, and in the `.asm` of the function they name when a detail is missing.

## Goals

- Four classes per city: rich, wealthy, poor, beggars. The current housed inhabitants become the poor.
- Three house types, as in P3: a house holds 40 rich, 70 wealthy or 140 poor. Each class has its own room.
- Consumption per class: each good has a weekly consumption per thousand inhabitants for each of the three housed classes, taken from P3's table. Beggars consume nothing, as in P3.
- One satisfaction per housed class (rich, wealthy, poor), computed every day as in P3.
- The satisfaction drives the population as in P3: how many poor rise into the wealthy and rich classes or fall back, whether the beggars are hired, and how many poor leave the city.
- The player can build each of the three house types. The city builds each type on its own when that class is crowded.
- The city panel shows the four counters.

## Non-goals

- The class benefits. They come later.
- P3's "Besoins des citoyens" game option. The level is fixed at Intermédiaire.
- The parts of P3's common satisfaction term that depend on systems DFM lacks: public buildings, events, famine, the unidentified town fields. Only the constant 9 is kept.
- P3's consumption modifiers: winter, missing grain, famine.
- Any display of the satisfaction to the player.
- Rent, or any income from houses.

## Anti-goals

- A satisfaction or class rule invented for DFM where P3 has one. If P3's rule cannot be carried over as it is, the builder stops and asks.
- A single consumption rate per inhabitant left in place beside the per-class rates. It goes.

## Decisions made with the user

- Three house types, as in P3.
- Consumption differs by class.
- The satisfaction level is fixed at P3's Intermédiaire: base −13 for the rich, −18 for the wealthy, −27 for the poor; it rises by 2 and falls by 1 per day.
- The player can build all three house types:

| House | Gold | Timber | Metal | Room |
|---|---|---|---|---|
| Poor | 5 000 | 10 | 10 | 140 |
| Wealthy | 10 000 | 15 | 20 | 70 |
| Rich | 25 000 | 25 | 30 | 40 |

The costs are the user's. The room is P3's and replaces the current house of 100 places.

- The player sees one counter per class and nothing else for now.

## The rules

### Population

- A city's population holds the rich, the wealthy, the poor and the beggars, and the room for each housed class. The "housed" inhabitants are the three housed classes together: the families of the workers, 4 per worker, as now.
- **Start** (P3, `Production et consommation des villes.md`, "Population de départ"): from the housed inhabitants the start gives today (4 per worker), rich = housed ÷ 10, wealthy = 3 × rich, poor = the rest; beggars as now. Each class gets as many of its houses as it needs, rounded up to whole houses.
- **Hiring** turns 4 beggars into 4 poor per worker, within the free room of the poor.
- **Leaving a job** (the reserve's leak): the 4 inhabitants per worker are taken from the rich, wealthy and poor in proportion to (count − 1) × (10, plus the opposite of the class's satisfaction when it is negative), as P3's `0x50E6D0`.

### Consumption

- Each good holds three weekly consumptions per thousand inhabitants: rich, wealthy and poor. The values are the "/sem." columns of P3's table in `Production et consommation des villes.md`, in the same units as DFM's goods. DFM's current single rate is P3's average at the start mix (1 rich, 3 wealthy, 6 poor): grain's 4.725 is (3.15 + 3 × 4.20 + 6 × 5.25) ÷ 10.
- A city's daily consumption of a good is the sum over the three classes. Beggars do not count.
- Everything that reads the consumption (thresholds, prices, starting stocks, the AI merchants, the market tab) keeps reading the city's total.

### Satisfaction

- Three values, one per housed class, starting at 50.
- Every day, as P3's `0x51CB80`: target = base of the class + the sum over the goods + 9.
  - For each good the inhabitants consume, with w the weight of the class for that good and T0 its first threshold: below T0, −w × (T0 − stock) ÷ T0; from T0 to 2 × T0, +w × (stock − T0) ÷ T0; above, +w.
  - The weights per good and class are P3's table in `Population des villes.md`. Pitch, pig iron, hemp, tiles and the weapons weigh 0.
  - The satisfaction moves towards 2 × target, by +2 or −1 a day, and stays between −40 and +80.

### What the satisfaction does

- **Rising and falling between classes**, as P3's `0x51C9A0`: the rich wanted = (satisfaction of the rich + 40) × poor ÷ 320, the wealthy wanted = (satisfaction of the wealthy + 40) × poor ÷ 160, each at least 1 and at most the room of the class (at least 10). A class below its target takes 2 × target ÷ count + 1 poor a day, if there are enough; a class above gives back 2 × count ÷ target + 1 to the poor.
- **The poor leave**, as P3's `0x51C9A0` / `0x51C6B0`: each day, max(1 % of the poor if their satisfaction is negative, (poor − room of the poor) ÷ 4 + 1). Above 3, that number ÷ 4 workers are taken away, and 4 poor leave the city per worker taken. They leave the city: they do not become beggars.
- **Hiring at production time** happens only when the satisfaction of the poor is positive, as P3's `0x510C30`. DFM keeps its check on the free room.

### Houses

- The city builds a house of a class when that class is above its share of its room: 97 % for the rich, 96 % for the wealthy, 95 % for the poor (P3's `0x51DBC0`). It takes the materials of the player's house of that type from its market, and pays no gold, as now.

### The day

The order of `CityMarket.StartDay` becomes, following P3's daily tick:

1. consumption, per class;
2. the city's workshops: hiring (only while the poor are satisfied), layoffs, production;
3. the satisfaction of the three classes;
4. the player's workshops fill their vacancies from the beggars;
5. rising and falling between classes, then the poor who leave;
6. the beggars come or go;
7. the city builds its houses.

## Display

- City panel: "Riches : 110 · Aisés : 330 · Pauvres : 660 · Mendiants : 64", in that fixed order (the parts of a sentence, not a list).
- City panel, buildings section, "Logements": one row and one button per house type, each with its own text, as the current row: "Maisons de pauvres : 2 (ville : 1 008 / 1 300 places)".

## Calls made by the designer, not by the user

- The satisfactions are whole numbers, as P3's words, and the goods term follows P3's integer arithmetic (`0x51CB80`'s `.asm`).
- Only the goods of the market count in the satisfaction: a switched-off good has no stock and no threshold, so it neither helps nor hurts.
- A city's house takes the materials of the player's house of the same type, without the gold, as a city's house does today.
- The house rows of the buildings section are a list of items: they are sorted with `NameOrder` ("Maisons d'aisés", "Maisons de pauvres", "Maisons de riches").
- The counters of the city panel keep the fixed order rich, wealthy, poor, beggars: they are the parts of one sentence.

## Left to the builder

- The structure of the code: where the satisfaction lives, how the rules are split into classes, how the assets hold the per-class values.
- Which workers are taken away when the poor leave. P3 takes the reserves first, then the workers above 30 per workshop, then the merchants' workshops; the builder carries this order over to DFM's reserves, city workshops and player workshops.
- How many houses the city may build in a day across the three types.
- The debug cities table: the columns that show the classes, their room and their satisfaction.
- Updating the existing goods and buildings assets, since `WorldMapSetup` leaves existing assets untouched, and the sample values in `WorldMapSetup` for a new scene.
- Updating `.claude/rules/population.md` and the other rules the change touches.
