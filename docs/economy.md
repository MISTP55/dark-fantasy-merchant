# L'économie de Dark Fantasy Merchant

Ce document explique comment fonctionne l'économie du jeu dans son état actuel : ce que les villes consomment et produisent, comment elles emploient leurs habitants, comment leur population évolue, comment les prix se forment et comment les marchands contrôlés par l'ordinateur décident de leurs voyages et de leurs cargaisons. Il est écrit pour quelqu'un qui découvre le jeu et n'a pas lu son code.

Presque toutes les règles reprennent celles de **Patrician III** (Ascaron, 2003), le jeu de commerce dont Dark Fantasy Merchant s'inspire, étudiées par la lecture de son code (dossier `patrician3-re\resultats\`). Les écarts avec lui sont signalés au fil du texte et récapitulés à la fin.

Tous les chiffres cités sont des réglages des fichiers de données du jeu (`Assets/Data`) : ils sont faits pour être ajustés en jouant, et ce document doit suivre leurs changements.

## Sommaire

1. [Vue d'ensemble](#1-vue-densemble)
2. [Le temps](#2-le-temps)
3. [Les marchandises](#3-les-marchandises)
4. [La consommation](#4-la-consommation)
5. [Les seuils de stock T0 à T3](#5-les-seuils-de-stock-t0-à-t3)
6. [Les prix](#6-les-prix)
7. [La production des ateliers](#7-la-production-des-ateliers)
8. [Les ouvriers](#8-les-ouvriers)
9. [La population](#9-la-population)
10. [Les bâtiments du joueur](#10-les-bâtiments-du-joueur)
11. [Les marchands IA](#11-les-marchands-ia)
12. [Le début d'une partie](#12-le-début-dune-partie)
13. [Différences avec Patrician III](#13-différences-avec-patrician-iii)
14. [Limites actuelles](#14-limites-actuelles)
15. [Où trouver chaque règle dans le code](#15-où-trouver-chaque-règle-dans-le-code)

---

## 1. Vue d'ensemble

Le monde compte 24 villes côtières, celles de la carte standard de Patrician III. Chacune a un **stock** de chaque marchandise. Ce stock baisse quand ses habitants consomment et monte quand ses ateliers produisent ou quand un navire lui vend des marchandises.

Un seul mécanisme règle presque tout : pour chaque marchandise, la ville calcule **quatre seuils de stock**, appelés T0, T1, T2 et T3, du plus bas au plus haut. La position du stock par rapport à ces seuils décide :

- du **prix** de la marchandise : cher quand le stock est bas, bon marché quand il est haut ;
- de l'**embauche** dans l'atelier qui la produit : il embauche beaucoup quand le stock est bas et cesse de produire quand il est trop haut ;
- des **transports** des marchands IA : ils prennent là où le stock est au-dessus des seuils et livrent là où il est en dessous.

La population, elle, suit les emplois et les logements : des mendiants arrivent en ville, les ateliers les embauchent s'il y a de la place pour les loger, et ils deviennent alors des habitants logés.

## 2. Le temps

- Une journée de jeu dure 30 secondes réelles ; l'avance rapide multiplie cette vitesse par 60.
- Une semaine compte 7 jours, un mois 30 jours.
- Les villes vivent **au jour le jour** : tout ce qui est décrit ci-dessous (consommation, embauche, production, arrivée des mendiants) se passe une fois par jour, au début de chaque journée.
- Les navires, eux, se déplacent en continu, et les marchands IA restent un jour dans chaque port où ils s'arrêtent.

## 3. Les marchandises

Le catalogue reprend les 20 marchandises de Patrician III, dans son ordre. **14 sont actives** en ce moment :

céréales, viande, poisson, bière, miel, vin, étoffe, peaux, huile, bois, métal, cuir, laine, poterie.

Les 6 autres (sel, épices, brai, fonte brute, chanvre, tuiles) sont désactivées : elles n'existent pas dans la partie, mais restent dans le catalogue et reviennent telles quelles si on les réactive.

**Unités.** Une marchandise se compte en **tonneaux**, ou en **boisseaux** pour les marchandises en vrac : céréales, viande, poisson, bois, laine (et, quand elles sont actives, fonte brute, chanvre et tuiles). Stocks, consommation, production, seuils et prix sont tous exprimés dans l'unité de la marchandise : le prix des céréales est celui d'un boisseau. Dans la cale d'un navire, un boisseau occupe la place de 10 tonneaux ; un navire marchand a une cale de 200 tonneaux.

**Ce qui définit une marchandise :**

| Donnée | Rôle | Exemple (céréales) |
|---|---|---|
| Prix de base | Le prix « normal », que les facteurs de prix multiplient (section 6) | 110 or le boisseau |
| Consommation | Ce que 1 000 habitants consomment par semaine | 4,725 boisseaux |
| Maximum d'ouvriers | La taille maximale de l'atelier qui la produit, la même dans toutes les villes | 68 |
| Production par ouvrier | Ce qu'un ouvrier de l'atelier de la ville produit par jour dans une ville qui la produit efficacement | 0,04333 boisseau |
| Production par ouvrier d'un marchand | Ce qu'un ouvrier d'un atelier du joueur produirait par jour dans la même ville, avant le bonus du nombre d'ateliers (section 10) | 0,03333 boisseau |
| Poids pour l'IA | L'importance que les marchands IA lui donnent (section 11) | 16 |

Les prix de base et les consommations sont ceux de Patrician III. Sa consommation, donnée par classe sociale (riches, aisés, pauvres), est ici moyennée sur sa répartition de départ : 10 % de riches, 30 % d'aisés, 60 % de pauvres.

## 4. La consommation

Chaque jour, au tout début de la journée, les habitants d'une ville consomment chaque marchandise :

> consommation du jour = consommation hebdomadaire pour 1 000 habitants ÷ 7 × habitants ÷ 1 000

Tous les habitants comptent, logés et mendiants. La consommation suit donc la population **du moment** : une ville qui grandit consomme davantage, et ses seuils (section 5), qui sont exprimés en jours de consommation, montent avec elle.

**Exemple.** Une ville de 1 067 habitants consomme chaque jour 4,725 ÷ 7 × 1,067 ≈ **0,72 boisseau de céréales** et 29,575 ÷ 7 × 1,067 ≈ **4,51 tonneaux de bière**.

La ville consomme dans la limite de son stock : un stock vide reste à zéro et la consommation du jour est simplement réduite. Un manque n'a pour l'instant aucune autre conséquence (pas de famine, pas de mécontentement).

Les stocks sont fractionnaires, puisqu'on consomme des fractions d'unité. Ce qui est affiché et ce qu'on peut acheter, c'est la partie entière : une ville qui a 4,8 boisseaux en vend au plus 4.

## 5. Les seuils de stock T0 à T3

### Le calcul

Pour chaque marchandise, chaque ville a quatre seuils. Ils sont recalculés à chaque fois qu'on en a besoin, donc suivent la population :

| Seuil | Formule | Plancher |
|---|---|---|
| **T0** | 7 jours de consommation | 5 unités |
| **T1** | T0 + 14 jours de consommation | 10 unités |
| **T2** | T1 + 10 jours de la **capacité** de l'atelier de la ville (rien à ajouter si la ville ne produit pas la marchandise) | 15 unités |
| **T3** | T2 + T0 | 20 unités |

La **capacité** d'un atelier est ce qu'il produirait par jour avec tous ses ouvriers (section 7). Elle ne dépend pas du nombre d'ouvriers qu'il a vraiment.

En résumé : T0 et T1 mesurent la **demande** de la ville (une semaine, puis trois semaines de consommation), T2 ajoute sa capacité de **production**, et T3 ajoute encore une semaine de demande au-dessus.

### Les planchers

Chaque seuil vaut **au moins son plancher**, en unités de la marchandise. Sans eux, une marchandise peu consommée aurait des seuils minuscules : avec T0 = 0,5 boisseau, la ville passerait de « pénurie totale » à « abondance » en une seule vente. Les planchers gardent une échelle de prix utilisable même dans un petit village ou pour une marchandise que personne ne consomme.

Les planchers **s'enchaînent** : chaque seuil se calcule à partir de la valeur *déjà relevée* du précédent, et T3 ajoute le T0 relevé.

```
T0 = max(7 jours × consommation,            5)
T1 = max(T0 + 14 jours × consommation,      10)
T2 = max(T1 + 10 jours × capacité,          15)
T3 = max(T2 + T0,                           20)
```

### Exemples dans une ville de 1 067 habitants

| Marchandise | Consommation par jour | T0 | T1 | T2 | T3 |
|---|---|---|---|---|---|
| Bière, produite par la ville | 4,51 | 31,6 | 94,7 | 324,7 | 356,3 |
| Bière, non produite | 4,51 | 31,6 | 94,7 | 94,7 | 126,2 |
| Céréales, produites | 0,72 | 5,04 | 15,1 | 34,9 | 40,0 |
| Céréales, non produites | 0,72 | 5,04 | 15,1 | 15,1 | 20,2 |
| Viande, produite | 0,24 | 1,66 → **5** | 8,3 → **10** | 16,9 | 21,9 |
| Viande, non produite | 0,24 | **5** | **10** | 10 → **15** | **20** |
| Laine, produite | 0,13 | 0,93 → **5** | 6,9 → **10** | 14,9 → **15** | **20** |
| Bois, produit | 0,30 | 2,1 → **5** | 9,2 → **10** | 48,4 | 53,4 |
| Poisson, non produit | 0,47 | 3,3 → **5** | 11,5 | 11,5 → **15** | **20** |

Les flèches montrent un seuil relevé par son plancher ; les valeurs en gras sont des planchers.

Comment lire la ligne « Viande, produite » : 7 jours de consommation ne font que 1,66 boisseau, donc T0 est relevé à 5. T1 vaut alors 5 + 14 × 0,24 = 8,3, relevé à 10. L'atelier de viande peut produire 0,69 boisseau par jour, donc T2 = 10 + 6,9 = 16,9, au-dessus du plancher. Enfin T3 = 16,9 + 5 = 21,9.

### Ce que les planchers changent

- **Les marchandises concernées.** Dans des villes d'un millier d'habitants, le plancher de T0 s'applique à toute marchandise consommée à moins de 0,71 unité par jour : actuellement la viande, le poisson, le bois et la laine. Les céréales sont juste à la limite : leur plancher cesse de jouer au-dessus d'environ 1 058 habitants. Plus une ville grandit, moins les planchers jouent.
- **La taille de la ville ne compte plus.** Quand tous les seuils d'une marchandise sont à leur plancher (la viande non produite ci-dessus), un village et une grande ville ont exactement les mêmes seuils, donc le même prix pour un même stock.
- **Des prix d'ouverture élevés.** Une ville commence la partie avec 7 jours de consommation de chaque marchandise (section 12), c'est-à-dire le T0 calculé *sans* plancher. Quand le plancher s'applique, ce stock est sous T0, donc cher. La viande ouvre avec 1,66 boisseau en stock, et la prochaine unité coûte environ 3,4 fois son prix de base, soit environ 3 275 or. La laine ouvre avec 0,93 boisseau : la ville n'a même pas une unité entière à vendre.
- **Une pente à la place d'un palier.** Pour une marchandise que la ville ne produit pas, T2 est normalement égal à T1. Le prix fait alors un saut à T1 : juste au-dessus, le facteur passe d'un coup à celui de T2 (section 6). Mais si T1 est sous 15, le plancher de T2 remplace ce saut par une pente. C'est le cas du poisson : le prix baisse progressivement entre 11,5 et 15 au lieu de sauter à 11,5. Dans une ville plus grande, où T1 dépasse 15, le saut revient.
- **Beaucoup de jours de stock.** Un producteur de laine se stabilise vers son T3 de 20 boisseaux, soit environ 150 jours de sa consommation (contre environ 80 jours pour la bière). Une ville remplie de viande jusqu'à son T1 de 10 boisseaux en a pour 42 jours. Ces marchandises circulent donc par gros lots espacés.

Patrician III a les mêmes planchers (5, 10, 15, 20). Ils y jouent moins souvent pour les matières premières, parce que la demande de ses villes comprend aussi ce que leurs ateliers consomment.

## 6. Les prix

### Le principe

> prix = prix de base × facteur

Le facteur ne dépend que du **stock de la ville** comparé à ses seuils. Il est donné en cinq points (stock vide, T0, T1, T2, T3), varie en ligne droite entre deux points et reste constant au-dessus de T3. Il y a deux courbes, vues du côté du joueur :

| Stock de la ville | vide | T0 | T1 | T2 | T3 et plus |
|---|---|---|---|---|---|
| Ce que paie le joueur qui **achète** | × 4,0 | × 1,5 | × 1,0 | × 0,8 | × 0,6 |
| Ce que touche le joueur qui **vend** | × 2,0 | × 1,4 | × 1,0 | × 0,7 | × 0,5 |

À T1, acheter et vendre se font au prix de base. Partout ailleurs, la ville vend plus cher qu'elle n'achète, et l'écart grandit aux extrêmes : commercer dans une ville en pénurie ou en surabondance coûte plus cher.

**Exemple : les céréales**, prix de base 110, dans une ville qui les produit (seuils 5,04 / 15,1 / 34,9 / 40,0). Ce sont les prix au point exact de chaque seuil ; le prix réel d'une unité s'en écarte un peu, comme l'explique la suite :

| Stock | Le joueur achète à | Le joueur vend à |
|---|---|---|
| vide | 440 | 220 |
| 5 (T0) | 165 | 154 |
| 15 (T1) | 110 | 110 |
| 35 (T2) | 88 | 77 |
| 40 et plus (T3) | 66 | 55 |

### Le prix de chaque unité

Une unité (un tonneau ou un boisseau) n'est pas payée au facteur du stock actuel, mais au **facteur moyen sur la tranche de stock qu'elle fait bouger**. Une unité achetée fait baisser le stock d'une unité : son prix est la moyenne du facteur entre le stock après l'achat et le stock avant. Une unité vendue fait de même entre le stock avant et le stock après la vente.

**Exemple.** Avec 15,1 boisseaux de céréales en stock (pile T1), la prochaine unité ne coûte pas 110 mais **113** : elle fait descendre le stock de 15,1 à 14,1, où le facteur est un peu plus haut.

Conséquences :

- Acheter plusieurs unités d'un coup revient à les acheter une par une : chacune fait monter le prix de la suivante. Vendre beaucoup fait baisser le prix de chaque unité suivante.
- Le prix d'achat est arrondi à l'or supérieur (au moins 1 or), celui de vente à l'or inférieur : **acheter puis revendre au même endroit ne rapporte jamais d'or**.
- Le prix affiché dans le marché d'une ville est celui de la prochaine unité.

### Le commerce du joueur

Quand un navire du joueur est au port, le joueur achète et vend depuis le panneau de la ville : 1 unité par clic, 10 avec Maj, 100 avec Ctrl. Une transaction de plusieurs unités s'arrête à la première qui n'est pas possible : stock épuisé, cale pleine ou or insuffisant. Un joueur endetté ne peut plus acheter, mais peut encore vendre.

## 7. La production des ateliers

### Les ateliers d'une ville

Chaque ville produit entre 6 et 10 marchandises, celles que Patrician III lui donne. Elle a un **atelier** par marchandise produite :

- en production **efficace** (3 à 6 marchandises par ville), chaque ouvrier produit la quantité de la marchandise ;
- en production **inefficace**, il en produit les trois quarts.

La **capacité** d'un atelier est maximum d'ouvriers × production par ouvrier. **Elle ne dépend pas de la taille de la ville** : un atelier de céréales efficace peut produire au plus 68 × 0,04333 ≈ 2,95 boisseaux par jour, dans une petite ville comme dans une grande.

Les marchandises qu'une ville ne produit pas, elle doit les recevoir des navires du joueur ou des marchands IA. En attendant, elle les paie cher.

### Le réglage de la production

La production par ouvrier de chaque marchandise est **celle de Patrician III** (`patrician3-re\resultats\Production et consommation des villes.md`). Patrician III compte en unités internes (200 pour un tonneau, 2 000 pour un boisseau) : un ouvrier de ses céréales en produit 86,7 par jour, soit 0,04333 boisseau.

| Marchandise | Production par ouvrier et par jour | Capacité mondiale ÷ consommation de départ |
|---|---|---|
| Céréales | 0,04333 boisseau | 2,2 |
| Viande | 0,01333 boisseau | 1,7 |
| Poisson | 0,03066 boisseau | 1,7 |
| Bière | 0,37591 tonneau | 1,65 |
| Miel | 0,08 tonneau | 1,9 |
| Vin | 0,268 tonneau | 2,95 |
| Étoffe | 0,15 tonneau | 1,7 |
| Peaux | 0,06667 tonneau | 1,75 |
| Huile | 0,135 tonneau | 1,5 |
| Bois | 0,08 boisseau | 7,8 |
| Métal | 0,2 tonneau | 1,8 |
| Cuir | 0,05332 tonneau | 1,7 |
| Laine | 0,02004 boisseau | 3,05 |
| Poterie | 0,067 tonneau | 1,75 |
| *Sel (inactif)* | 0,167 tonneau | 58 |
| *Chanvre (inactif)* | 0,00833 boisseau | 13,5 |
| *Tuiles (inactives)* | 0,067 boisseau | 317 |
| *Brai, fonte brute (inactifs)* | 0,067 tonneau, 0,01667 boisseau | personne n'en consomme |

La dernière colonne compare ce que le monde peut produire, tous ses ateliers au maximum (une production inefficace comptant 0,75), à ce que consomment ses habitants au début de la partie. Quelques cas particuliers :

- Le **cuir** n'a pas d'atelier à lui dans Patrician III : l'élevage fait de la viande et, en plus, du cuir à 40 % de la viande en unités internes. Son ouvrier fait donc 40 % de 26,7 unités, soit 0,05332 tonneau.
- L'**huile** de baleine sort de la pêcherie de Patrician III, à 27 unités par ouvrier dans une ville qui a la baleine en production efficace, soit 0,135 tonneau.
- Les **épices** ne sont produites nulle part dans Patrician III : leur valeur (0,02038 tonneau) n'a pas de modèle, et elles sont inactives.

Patrician III dimensionne ses matières premières et ses matériaux de construction sur la demande totale : celle des habitants, mais aussi celle des ateliers qui les transforment (la bière prend des céréales et du bois, l'étoffe de la laine, le métal du bois et de la fonte brute…) et celle de la construction. Dark Fantasy Merchant n'a encore ni ateliers qui consomment des matières premières, ni construction par les villes : ces marchandises sont donc **en gros surplus** (le bois à 7,8, la laine à 3, le sel à 58, les tuiles à 317). Ce surplus est voulu pour l'instant : il se résorbera quand les intrants et les chaînes de production seront en place. En attendant, la régulation par le stock (ci-dessous) l'empêche de s'accumuler : les producteurs se stabilisent sur de gros stocks, à bas prix.

### La régulation par le stock

Chaque jour, après la consommation, chaque atelier ajuste son nombre d'ouvriers selon le stock restant de sa marchandise :

| Stock restant | Effet sur l'effectif visé |
|---|---|
| T0 ou moins | +16 ouvriers |
| au-dessus de T0, jusqu'à T1 | +8 |
| au-dessus de T1, jusqu'à T2 | +4 |
| au-dessus de T2, jusqu'à T3 | +1 |
| au-dessus de T3 | **retombe à 1 ouvrier**, et l'atelier ne produit rien ce jour-là |

Un stock égal à un seuil compte comme en dessous. L'effectif visé ne dépasse jamais le maximum de l'atelier. L'atelier produit ensuite avec les ouvriers qu'il a pour la journée.

À retenir :

- **Sous T3, un atelier ne licencie jamais** : il ne fait qu'embaucher, plus vite quand le stock est bas. Il cherche donc à tourner à plein régime tant que sa marchandise n'est pas en abondance.
- **Au-dessus de T3, il s'effondre d'un coup** à un seul ouvrier et cesse de produire. Les autres partent en réserve (section 8).
- Un producteur laissé seul voit donc son stock osciller entre T2 et T3 : il remplit, dépasse T3, s'arrête, retombe sous T3 par la consommation, réembauche.
- L'atelier ne prend que les ouvriers qu'il trouve (section 8). En pratique, ce qui le freine est la main-d'œuvre disponible, et il atteint rarement son maximum.

Cette règle est exactement celle de Patrician III.

### Exemple d'une journée d'atelier

Un atelier de bière efficace a 40 ouvriers et une réserve de 3 licenciés. Après la consommation du jour, il reste 80 tonneaux, entre T0 (31,6) et T1 (94,7) : l'effectif visé est 40 + 8 = 48.

1. Il reprend ses 3 réservistes (43 ouvriers).
2. La ville a 60 mendiants et 20 places libres dans ses maisons : elle peut loger au plus 20 ÷ 4 = 5 familles d'ouvriers. Il embauche 5 mendiants comme ouvriers (48 ouvriers).
3. Il produit 48 × 0,338 ≈ 16,2 tonneaux.

## 8. Les ouvriers

### Un ouvrier, une famille

Chaque ouvrier représente une famille de **4 habitants logés**. Embaucher un ouvrier parmi les mendiants fait passer 4 mendiants au statut d'habitants logés, ce qui demande 4 places libres dans les maisons de la ville.

### L'embauche par les ateliers de la ville

Quand un atelier veut plus d'ouvriers (section 7), il les cherche dans cet ordre :

1. **Sa propre réserve**, c'est-à-dire ses ouvriers licenciés.
2. **Les mendiants**, dans la limite des logements libres : au plus mendiants ÷ 4 ouvriers et places libres ÷ 4 ouvriers. **Sans place libre, aucun mendiant n'est embauché.**
3. **Les réserves des autres ateliers** de la ville, dans l'ordre du catalogue.

Ce qui manque encore n'est pas embauché ce jour-là ; l'atelier réessaiera le lendemain.

### Le licenciement et la réserve

Quand un atelier licencie (son stock a dépassé T3), ses ouvriers passent dans sa **réserve**. Ils restent logés, continuent de consommer et seront les premiers repris quand l'atelier réembauchera. La réserve n'a pas de limite.

Une petite partie de la réserve retourne lentement à la rue. À chaque licenciement, l'atelier ajoute 2 % de sa réserve à un compteur. Dès que ce compteur atteint un ouvrier entier, cet ouvrier quitte la réserve et sa famille redevient 4 mendiants. Mais cette fuite ne joue que **tant que les ouvriers et la réserve de l'atelier font moins de 30 au total**. Un atelier plus gros garde toute sa réserve.

Concrètement, la main-d'œuvre d'une ville est un stock à peu près fixe, qui passe d'un atelier à l'autre par les réserves : quand un atelier s'arrête, ses ouvriers vont dans sa réserve et un autre atelier peut les reprendre.

### Le nombre d'ouvriers d'une ville peut-il grandir ?

Oui, mais **le plafond est le logement**. Le code garantit en permanence que :

> habitants logés = 4 × (ouvriers des ateliers de la ville + leurs réserves + ouvriers des ateliers du joueur)

Comme les logés ne peuvent pas dépasser le nombre de places dans les maisons, une ville a au plus **places ÷ 4** ouvriers, réservistes compris.

- **Chaque maison**, du joueur ou de la ville, ajoute 100 places, donc 25 ouvriers possibles. Les ateliers de la ville en profitent avant ceux du joueur (section 10).
- **Quand ses logements sont presque pleins**, la ville construit elle-même une maison, si son marché a le bois et le métal nécessaires (section 9). Exemple : une ville de 6 ateliers commence avec 252 ouvriers, soit 1 008 logés dans 1 100 places (91,6 %). Dès qu'elle dépasse 1 045 logés, elle construit une maison et passe à 1 200 places.
- La vitesse de croissance est aussi limitée par les mendiants disponibles : une soixantaine dans une ville d'un millier d'habitants, puis environ 7 nouveaux par jour (section 9).
- La somme des maximums des ateliers, plus les emplois des ateliers du joueur, forme un plafond absolu.

Comme dans Patrician III, une ville grandit donc toute seule, tant qu'elle a les matériaux de ses maisons.

## 9. La population

### Logés et mendiants

Les habitants d'une ville sont de deux sortes :

- les **logés**, qui vivent dans les maisons de la ville : ce sont les familles des ouvriers ;
- les **mendiants**, qui n'ont pas de logement.

Population = logés + mendiants. Tous consomment. La nourriture ne fait ni grandir ni diminuer la population : seul le travail la fait bouger.

### Les mendiants vont et viennent

Les mendiants sont **la seule source de nouveaux habitants**. Chaque jour, leur nombre tend vers une **cible** qui ne dépend que de la population P :

> cible = √(P × 50 ÷ 2) ÷ 3 + 8 (les racines et divisions sont arrondies à l'entier inférieur)

- Sous la cible, ⌈√P ÷ 5⌉ mendiants arrivent dans la journée.
- Au-dessus, ⌈√P ÷ 10⌉ s'en vont.
- Jamais au-delà de la cible.

**Exemple.** Dans une ville de 1 067 habitants, la cible est de 62 mendiants. S'il en manque, jusqu'à 7 arrivent par jour ; s'il y en a trop, jusqu'à 4 partent par jour.

La cible grandit comme la racine carrée de la population : une grande ville garde proportionnellement moins de mendiants en attente.

### Comment une ville grandit

Le cycle de la croissance :

1. La ville a des **places libres** dans ses maisons, parce que le joueur ou la ville elle-même y a construit des maisons.
2. Un atelier veut **embaucher** (son stock est bas), ou un atelier du joueur a des postes vacants.
3. Des mendiants sont embauchés : ils deviennent des habitants logés.
4. Le nombre de mendiants retombe sous sa cible : de **nouveaux mendiants arrivent**.

La population a donc grandi du nombre de mendiants arrivés. Sans places libres ou sans emplois, ce cycle s'arrête et la population reste stable autour de « logés + cible des mendiants ».

### La ville construit ses maisons

À la fin de chaque journée, la ville compare ses **logés** à ses **places**. Les mendiants ne comptent pas : ils n'occupent pas de place.

- Si les logés dépassent **95 % des places** (strictement : 95 % pile ne suffit pas), et si le marché de la ville a en stock tous les matériaux d'une maison, la ville en construit une.
- Sa maison est celle du joueur : mêmes matériaux (10 boisseaux de bois et 10 tonneaux de métal), mêmes 100 places, prête tout de suite. La ville ne paie pas d'or : elle n'en a pas.
- Les matériaux sont retirés du stock **même si cela le vide**. Le prix du bois et du métal monte donc juste après. Ce retrait n'est pas compté comme une consommation dans les chiffres de la semaine, comme les achats du joueur.
- Si un des matériaux manque, rien n'est construit ni retiré ce jour-là.
- **Une maison par jour au plus.**
- Un matériau désactivé dans le catalogue est simplement ignoré : la maison ne demande que les matériaux actifs, et rien du tout s'ils sont tous désactivés. C'est la règle de toutes les constructions (section 10).

**Exemple.** Une ville a 1 046 logés dans 1 100 places (95,1 %), et 12 boisseaux de bois et 10 tonneaux de métal au marché. Le soir, elle construit une maison : elle a maintenant 1 200 places, 2 boisseaux de bois et plus de métal. Le lendemain, ses logés sont à 87 % : elle ne construira plus avant d'avoir dépassé 1 140 logés.

Pour le joueur, c'est une **course** : ses maisons lui rapporteront plus tard un loyer et de la réputation, celles de la ville ne lui rapportent rien. Pour en profiter, il doit construire avant que la ville ne dépasse 95 %.

Le seuil de 95 % est un réglage de `Economy.asset` (`cityHouseOccupancy`, section « Cities' houses »), entre 0 et 1. Les maisons construites par la ville sont comptées à part de celles du joueur ; l'outil de débogage (F1, onglet « Villes ») les montre entre parenthèses après les logements : « 1 046 / 1 200 (+1) ».

### Comment une ville diminue

Presque jamais pour l'instant. Seuls les départs de mendiants au-dessus de leur cible font baisser la population. La fuite des réserves (section 8) ne fait que transformer des logés en mendiants, sans changer le total.

### L'ordre d'une journée de ville

1. Les habitants consomment chaque marchandise.
2. Pour chaque marchandise, dans l'ordre du catalogue : l'atelier de la ville embauche ou licencie (section 8), puis produit (section 7).
3. Les ateliers du joueur pourvoient leurs postes vacants parmi les mendiants (section 10).
4. Les mendiants arrivent ou partent.
5. Si ses logements sont presque pleins, la ville construit une maison.

## 10. Les bâtiments du joueur

Le joueur peut construire deux bâtiments dans une ville, depuis le panneau de cette ville :

| Bâtiment | Or | Bois | Métal | Effet |
|---|---|---|---|---|
| Maison | 5 000 | 10 boisseaux | 10 tonneaux | 100 places de logement |
| Atelier | 10 000 | 10 boisseaux | 30 tonneaux | 30 emplois, pour une marchandise que la ville produit |

Les matériaux sont pris d'abord dans la cale du navire du joueur au port, puis achetés au marché de la ville. La construction se fait en entier ou pas du tout.

**Toute construction**, du joueur ou d'une ville, ignore les matériaux désactivés dans le catalogue : elle ne demande que ses matériaux actifs, et seulement son or s'ils sont tous désactivés. Si le métal est désactivé, une maison coûte 5 000 or et 10 boisseaux de bois, un atelier 10 000 or et 10 boisseaux de bois ; si le bois et le métal le sont tous les deux, ils ne coûtent que leur or. Le panneau n'affiche que les matériaux demandés. Un bâtiment n'est indisponible que si son coût en or est négatif, une erreur de réglage.

**Les maisons** ajoutent leurs places à celles de la ville, pour n'importe quel habitant : ce sont elles qui permettent à la ville de grandir (section 9).

**Les ateliers du joueur** embauchent après ceux de la ville, uniquement parmi les mendiants, dans l'ordre où ils ont été construits :

- il faut au moins 5 mendiants et plus de 3 places libres ;
- la ville en fournit alors ⌊min(mendiants − 1, places libres) ÷ 4⌋ ouvriers par jour, au premier atelier construit jusqu'à ce qu'il soit plein, puis au suivant ;
- ils ne licencient jamais et n'ont pas de réserve.

Comme les ateliers de la ville embauchent en premier, ceux du joueur ne trouvent des ouvriers que s'il reste des mendiants et des places après eux. Construire des maisons en même temps que des ateliers est donc indispensable.

### Ce que vaut un atelier du joueur

Comme dans Patrician III, un ouvrier d'un atelier du joueur ne produit pas autant qu'un ouvrier de l'atelier de la ville. Patrician III donne à chaque atelier de ville un **coefficient** propre à la marchandise (4 pour le vin, 2 pour les peaux et le métal, 1,3 pour les céréales…), que les ateliers des marchands n'ont pas. À la place, un marchand qui a plusieurs ateliers de la même marchandise dans une ville reçoit un **bonus** :

| Ateliers de la marchandise dans la ville | Bonus |
|---|---|
| 1 ou 2 | aucun |
| 3 à 5 | + 3 % |
| 6 à 8 | + 6 % |
| 9 ou plus | + 10 % |

Production d'un jour = ouvriers de tous ses ateliers de la marchandise × production par ouvrier d'un marchand × bonus. Comme pour la ville, une ville qui produit la marchandise inefficacement donne les trois quarts.

| Marchandise | Ouvrier de la ville | Ouvrier d'un marchand | Rapport |
|---|---|---|---|
| Vin | 0,268 tonneau | 0,067 tonneau | ÷ 4 |
| Peaux | 0,06667 tonneau | 0,03333 tonneau | ÷ 2 |
| Métal | 0,2 tonneau | 0,1 tonneau | ÷ 2 |
| Bière | 0,37591 tonneau | 0,235 tonneau | ÷ 1,6 |
| Viande | 0,01333 boisseau | 0,00833 boisseau | ÷ 1,6 |
| Cuir | 0,05332 tonneau | 0,03333 tonneau | ÷ 1,6 |
| Étoffe | 0,15 tonneau | 0,1 tonneau | ÷ 1,5 |
| Céréales | 0,04333 boisseau | 0,03333 boisseau | ÷ 1,3 |
| Miel | 0,08 tonneau | 0,06667 tonneau | ÷ 1,2 |
| Bois | 0,08 boisseau | 0,06667 boisseau | ÷ 1,2 |
| Laine | 0,02004 boisseau | 0,0167 boisseau | ÷ 1,2 |
| Poisson | 0,03066 boisseau | 0,03333 boisseau | × 1,09 |
| Huile, poterie (et sel, brai, fonte brute, chanvre, tuiles, inactifs) | — | la même | identique |

*Exemple* : trois ateliers de céréales du joueur, 90 ouvriers en tout, dans une ville qui produit les céréales efficacement : 90 × 0,03333 × 1,03 ≈ 3,09 boisseaux par jour. L'atelier de la ville, à ses 68 ouvriers, en fait 2,95.

Les épices, produites nulle part dans Patrician III, ont la même valeur pour les deux.

⚠️ **Les ateliers du joueur ne produisent encore rien** : ils emploient des ouvriers, et le jeu sait calculer ce qu'ils produiraient (`CityMarket.PlayerDailyOutputOf`), mais cette production n'entre encore dans aucun stock. Dans Patrician III, elle va dans le comptoir du marchand, que Dark Fantasy Merchant n'a pas encore.

## 11. Les marchands IA

### Qui ils sont

- Deux navires par ville, soit **48 navires**, nommés « Marchand indépendant 1 », « Marchand indépendant 2 », etc., numérotés dans l'ordre des villes : les deux de Bruges sont les numéros 1 et 2.
- Chacun a une **ville d'attache**, celle où il est apparu. Elle **ne change jamais** : le navire y revient toujours.
- Leur or est **illimité** : ils n'ont pas de trésor, ne paient pas de salaires, et seul leur bénéfice cumulé est compté (visible dans l'outil de débogage, touche F1).
- Ils achètent et vendent aux mêmes prix que le joueur (section 6) : leurs achats font monter les prix là où ils chargent, leurs ventes les font baisser là où ils livrent.
- Leur équipage de 14 marins sur 28 leur donne la pleine vitesse. Ils restent un jour dans chaque port où ils s'arrêtent.

### Ce qu'ils cherchent

Les marchands IA ne cherchent pas le plus gros bénéfice. Ils **approvisionnent** : ils déplacent les marchandises des villes qui en ont trop vers celles qui vont en manquer, avec deux limites :

- une ville source ne cède jamais de quoi passer sous le **milieu entre son T0 et son T1** ;
- une ville destinataire n'est jamais remplie au-delà de son **T1**.

### Le tour d'un navire

1. **Au port d'attache**, le navire vend toute sa cargaison, choisit une **destination finale** et charge pour elle.
2. **L'aller** : il va directement à sa destination finale, **sans escale**.
3. **À la destination finale**, il vend **toute** sa cargaison, quels que soient les prix devenus. Sa prochaine destination finale est alors sa ville d'attache.
4. **Il recharge sur place** : d'abord ce qui manque à sa ville d'attache, puis, s'il a trouvé une escale et qu'il lui reste plus d'un quart de cale libre, ce qui manque à cette escale. Avec un quart de cale libre ou moins, il renonce à l'escale et rentre directement.
5. **Le retour** : il peut s'arrêter à plusieurs **escales**. Dans chacune, il vend seulement de quoi amener la ville à son T1, en gardant ce qui manque à sa ville d'attache. Puis il recharge pour sa ville d'attache et pour l'escale suivante, comme à l'étape 4.
6. **De retour au port d'attache**, il vend tout et recommence. S'il ne trouve aucune destination qui vaille le voyage, il attend un jour de plus au port et réessaie.

Si un navire se retrouve immobile en mer sans ordre (une route qui a échoué, par exemple), il est renvoyé au dernier port où il a mouillé, puis reprend son tour vers sa ville d'attache.

### Évaluer une marchandise sur un trajet

Toutes les décisions reposent sur une même évaluation : pour une marchandise, d'une ville source vers une ville destinataire, combien transporter et quel **score** cela vaut.

**1. Le stock projeté de la destination.** Le navire ne regarde pas le stock actuel de la destination, mais celui qu'elle aura à son arrivée :

> stock projeté = stock actuel + ce que les marchands IA lui apportent déjà + (production − consommation) × jours de trajet

La production est celle de ses ouvriers du moment. « Ce que les marchands IA lui apportent déjà », c'est tout ce que les navires IA ont chargé pour cette ville et pas encore livré, y compris ce que porte le navire qui décide. Ainsi, deux navires ne comblent pas le même manque, et un navire ne rachète pas ce qu'il transporte déjà.

**2. La quantité.** Elle se calcule à partir des seuils des deux villes :

- **rien** si la source est au milieu entre son T0 et son T1 ou en dessous, ou si la destination atteindra son T1 ;
- si l'excédent de la source au-dessus de son T1 suffit à amener la destination à son T1, le navire transporte exactement ce manque ;
- sinon, la quantité se construit en trois parts :
  1. tout l'excédent de la source au-dessus de son T1 ;
  2. de quoi amener la destination à un **point cible**, situé aux 3/8 de l'écart entre son T0 et son T1, sans faire passer la source sous son milieu ;
  3. le reste est partagé pour que les deux villes finissent à la **même position relative**, la source entre son milieu et son T1, la destination entre son point cible et son T1.

La quantité est arrondie à l'unité inférieure. Un lot de moins de **5 tonneaux** ou **3 boisseaux** est ignoré.

**3. Le score.**

> score = (facteur auquel la destination achète, à son stock projeté − facteur auquel la source vend, à son stock actuel) × poids de la marchandise

Les facteurs sont ceux des courbes de prix (section 6), pris en un seul point. Un score nul ou négatif annule le transport.

Le score compare des **facteurs**, pas des prix en or : le prix de base n'entre pas en compte. Un boisseau de laine à 880 or pèse autant qu'un tonneau de bière à 34,8 or au même écart de facteurs. C'est le **poids** qui donne la priorité :

| Poids | Marchandises actives |
|---|---|
| 16 | céréales |
| 11 | bois, métal, laine |
| 9 | viande, poisson, bière, miel, vin, étoffe, peaux, huile, cuir, poterie |

La durée du trajet ne divise rien : une ville lointaine vaut une ville proche au même score.

**Exemple 1 : un gros excédent.** Deux villes de 1 067 habitants ont les mêmes seuils de céréales (T0 = 5,04, T1 = 15,1, milieu 10,1). La ville A les produit (T2 = 34,9, T3 = 40,0) et en a 38 boisseaux. La ville B ne les produit pas et en aura 2 à l'arrivée.

- B a besoin de 15,1 − 2 = 13,1 boisseaux pour atteindre son T1. L'excédent de A au-dessus de son T1 est de 22,9 : il suffit. Le navire transporte **13 boisseaux**.
- Facteur auquel B achète à 2 boisseaux : 1,76. Facteur auquel A vend à 38 boisseaux : 0,68. Score : (1,76 − 0,68) × 16 ≈ **17,3**.

**Exemple 2 : un petit excédent.** Mêmes villes, mais A n'a plus que 18 boisseaux.

1. Excédent de A au-dessus de son T1 : 18 − 15,1 = 2,9.
2. Point cible de B : 5,04 + 3/8 × (15,1 − 5,04) = 8,8. B en aurait 2 + 2,9 = 4,9 : il lui faut 3,9 de plus, et A peut les céder sans passer sous son milieu.
3. A est alors à 11,2 et B à 8,8. Le partage donne 0,6 de plus, et les deux villes finissent au même dixième de leur plage.

Total : 2,9 + 3,9 + 0,6 = 7,4, soit **7 boisseaux**.

### Choisir une destination finale

Au port d'attache, le navire évalue chaque autre ville qu'il peut atteindre. Pour chaque marchandise, il prend le score de ce qu'il pourrait y porter ; si elle n'a rien à y porter, il prend le score de ce qu'il pourrait en ramener. Il additionne ces scores et retient la ville au **total le plus haut**. Si aucune ville n'a un total positif, il attend.

### Choisir une escale

À chaque port du retour, le navire cherche une escale vers sa ville d'attache. Une ville peut servir d'escale si :

- ce n'est ni le port actuel, ni la ville d'attache, ni une ville déjà visitée pendant ce retour ;
- le détour (port actuel → escale → ville d'attache) fait **moins de 1,2 fois** le trajet direct.

Parmi ces villes, il retient celle où ce qu'il pourrait porter depuis le port actuel a le meilleur total de scores, et, à égalité, celle qui allonge le moins le trajet. S'il n'y en a aucune, il rentre directement.

### Charger

Pour charger vers une ville, le navire prend les marchandises par **score décroissant**, chacune dans la limite de sa quantité, du stock de la source et de la place restante dans la cale.

### Le début de partie

Au départ, toutes les villes ont 7 jours de stock de chaque marchandise, au niveau de leur T0. Aucune ville n'est au-dessus du milieu entre son T0 et son T1 : aucun transport n'est possible, et les marchands IA attendent au port les premiers jours, le temps que les producteurs remplissent leurs stocks.

## 12. Le début d'une partie

Une nouvelle partie démarre comme dans Patrician III, en partant des ouvriers :

1. **Les ouvriers.** Chaque ville reçoit 250 ouvriers, répartis à parts égales entre ses ateliers. Chaque part est arrondie à l'entier supérieur et plafonnée au maximum de l'atelier. Une ville de 6 ateliers donne ainsi 42 ouvriers à chacun, 252 en tout.
2. **La population.** Pour W ouvriers : 4 × W habitants logés, et 4 × W ÷ 17 mendiants, arrondi à l'inférieur. Pour 252 ouvriers : 1 008 logés et 59 mendiants, soit **1 067 habitants**.
3. **Les maisons.** Des maisons de 100 places, en nombre suffisant pour loger tout le monde : 1 100 places pour 1 008 logés.
4. **Les stocks.** 7 jours de consommation de chaque marchandise, produite ou non. Les prix ouvrent donc à 1,5 fois le prix de base à l'achat et 1,4 fois à la vente, sauf pour les marchandises dont le seuil T0 est relevé par son plancher, qui ouvrent plus cher (section 5). Les villes productrices se remplissent et les autres se vident dans la première semaine.

Toutes les villes ont donc presque la même taille au départ : elles ne diffèrent que par l'arrondi de la répartition des ouvriers.

## 13. Différences avec Patrician III

| Sujet | Patrician III | Dark Fantasy Merchant |
|---|---|---|
| Production par ouvrier | Propre à chaque atelier, aucun équilibre global | La même ; les matières premières sont en surplus tant que les ateliers n'en consomment pas |
| Ateliers qui consomment des matières premières | Oui (bière : céréales et bois, métal : bois et fonte…) | Pas encore |
| Hiver, récoltes, approvisionnement caché | Oui | Non |
| Consommation des mendiants | Ils ne consomment pas | Ils consomment comme les autres |
| Classes sociales | Riches, aisés, pauvres, avec une satisfaction par classe | Une seule classe moyenne, sans satisfaction |
| Embauche parmi les mendiants | Si la satisfaction des pauvres est positive, sans regarder les logements ; les pauvres sans logement quittent ensuite la ville | Seulement dans la limite des places libres |
| Embauche au-delà de 30 ouvriers par atelier | Passe par la liste des postes vacants, pourvue parmi les mendiants après la production, comme pour les ateliers des marchands | Comme en dessous de 30 |
| Maisons | La ville construit elle-même quand ses logements sont presque pleins | La ville construit elle-même au-delà de 95 % de ses places, une maison par jour au plus, avec les matériaux de la maison du joueur |
| Ouvriers de départ | 250 + 2,5 par année après 1300 (jusqu'à 500), répartis selon un poids par atelier, plus 25 dans des emplacements particuliers | 250, répartis à parts égales |
| Or des marchands IA | Ils paient 5/6 du prix et touchent 6/5 des ventes | Or illimité, prix normaux, bénéfice seulement compté |
| Comptoirs et ateliers des marchands IA | Oui | Non |
| Choix de l'escale par l'IA | Pratiquement arbitraire | Celle qui a le meilleur score |
| Ce qu'un navire IA porte déjà pour sa ville d'attache | Pas compté quand il est en escale : il peut racheter le même manque | Compté |

## 14. Limites actuelles

- Les **ateliers du joueur ne produisent rien** pour l'instant : leur production est calculée (section 10), mais elle n'a pas encore de comptoir où entrer.
- **Aucun manque n'a de conséquence** sur la population : pas de famine, pas de satisfaction, pas de départ d'habitants.
- Les **ateliers ne consomment aucune matière première**, et le brai, la fonte brute, le chanvre et les tuiles, qui ne servent qu'à ça ou à la construction, sont désactivés.
- Les marchands IA n'ont ni comptoir, ni atelier, ni or limité.

## 15. Où trouver chaque règle dans le code

Les règles sont dans `Assets/Scripts/Core`, les réglages dans `Assets/Data`.

| Règle | Code | Réglages |
|---|---|---|
| Journée d'une ville, stocks, consommation, embauche, production | `CityMarket` | `Economy.asset`, `Goods/*.asset` |
| Seuils et planchers | `ThresholdRule` | `Economy.asset` |
| Régulation des ateliers par le stock, répartition des ouvriers de départ | `WorkforceRule` | `Economy.asset` |
| Mendiants, embauche dans les logements, fuite des réserves | `LabourRule` | `Economy.asset` |
| Population, logés, places | `CityPopulation` | `Buildings.asset` (places d'une maison) |
| Courbes et calcul des prix | `PriceCurve`, `MarketPricing` | `Economy.asset` |
| Achats et ventes | `Trade` | |
| Bâtiments du joueur | `Construction` | `Buildings.asset` |
| Maisons construites par la ville | `CityHousingRule`, `CityMarket.StartDay` | `Economy.asset` (seuil), `Buildings.asset` (matériaux et places) |
| Ce que produiraient les ateliers du joueur | `MerchantOutputRule`, `CityMarket.PlayerDailyOutputOf` | `Economy.asset` (bonus), `Goods/*.asset` (production par ouvrier d'un marchand) |
| Tour des marchands IA | `AiMerchantFleet` | `AI/AiMerchants.asset` |
| Choix des destinations, des escales et des cargaisons | `SupplyPlanner` | poids dans `Goods/*.asset` |
| Quantité à transporter | `ShipmentQuantity` | |
