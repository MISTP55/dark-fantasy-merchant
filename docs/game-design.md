# Dark Fantasy Merchant — Document de conception

Ce document décrit la vision du jeu : son univers, sa progression et ses grands systèmes. Les specs techniques de chaque système sont dans `docs/superpowers/specs/`.

## Intention

Dark Fantasy Merchant est un jeu de gestion et de simulation de commerce maritime, sur PC. Le joueur incarne un marchand d'une des dernières cités côtières encore debout dans un monde envahi par les démons. Par le commerce, il fait vivre sa cité, gravit la hiérarchie sociale jusqu'à la gouverner, puis mène la reconquête des terres perdues.

## Univers

Une invasion démoniaque a déferlé sur le monde. Les grandes villes de l'intérieur sont tombées les unes après les autres. Seules quelques cités côtières résistent encore. Elles doivent leur survie à la mer : coupées de l'intérieur, elles commercent entre elles par bateau et s'échangent ce que chacune ne peut plus produire seule.

Les démons ne sont pas cantonnés à la terre ferme : ils rôdent aussi sur les mers, et chaque traversée comporte un risque.

## La menace démoniaque

### Sièges et chute des villes

Les démons assiègent les cités. Une ville dont la défense est insuffisante finit parfois par tomber.

Une ville conquise est temporairement hors jeu : on ne peut plus y commercer. Les réfugiés qui ont survécu la rebâtissent ensuite, et elle redevient une cité après un certain temps.

En début de partie, les villes sont assez défendues pour résister aux premiers sièges. Le joueur a le temps d'apprendre le commerce avant que la menace ne devienne pressante.

### Pollution

Les ateliers et les industries des villes fonctionnent au mana. Le mana est une ressource propre à chaque ville : il ne se transporte pas et ne se vend pas. Aujourd'hui, la production d'un atelier dépend de la main-d'œuvre, recrutée parmi les mendiants. Ce mécanisme sera remanié pour y intégrer le mana.

C'est surtout la consommation de mana qui produit la pollution, et la pollution rend les démons de plus en plus agressifs. Plus une ville s'industrialise, plus elle attire la menace.

C'est la tension centrale du développement : le joueur qui fait croître une ville sans renforcer sa défense risque de la voir tomber, et de perdre les industries qu'il y a bâties.

## Déroulement d'une partie

Une partie typique suit trois phases. Chacune s'appuie sur ce que le joueur a bâti dans la précédente.

### 1. L'exploration

Le joueur commence dans sa ville natale avec un navire. Il ne connaît pas les autres cités : il doit les découvrir en naviguant pour trouver des partenaires commerciaux.

Pendant cette phase, le joueur apprend progressivement :

- les marchandises, et l'utilité de chacune pour les villes ;
- comment gagner de l'argent en commerçant, en achetant là où une marchandise abonde et en la revendant là où elle manque ;
- le combat naval, contre les démons qui hantent les mers.

L'argent gagné lui permet de construire d'autres navires, de les armer et d'embaucher des capitaines. Un capitaine permet de confier à un navire une route commerciale automatique. Le joueur passe alors de marchand à armateur.

### 2. Le développement de la cité

Le commerce bâtit la réputation du joueur. Une réputation suffisante lui vaut un permis de construire dans sa ville natale, et éventuellement dans d'autres villes. Il peut alors y construire ses propres ateliers, produire lui-même ses marchandises et en vendre toujours plus.

Avec une réputation plus grande encore, le joueur peut briguer la direction de sa ville et en devenir le maire. Les villes du jeu ne sont probablement pas des démocraties : le mode de désignation reste à définir.

La fonction de maire débloque toute la gestion de la ville :

- la construction des bâtiments publics ;
- l'agrandissement des murs ;
- l'organisation de la défense de la ville.

Ces chantiers sont soutenus par l'empire commercial que le joueur a bâti jusque-là.

### 3. La reconquête

Cette phase de fin de partie est encore à préciser. Pistes envisagées :

- lever des armées ;
- fédérer les autres cités côtières ;
- reconquérir les territoires perdus de l'intérieur ;
- fonder de nouvelles villes dans les terres intérieures, par la colonisation.

## Questions ouvertes

- **Démons en mer** : quelle forme prend la menace sur les mers (flottes, créatures, zones dangereuses) ?
- **Mana** : d'où vient-il, et que représente-t-il dans l'univers ? Remplace-t-il la main-d'œuvre comme limite de production, ou s'y ajoute-t-il ? Est-il en quantité fixe, ou se régénère-t-il ? Le joueur peut-il en augmenter la réserve d'une ville ? Ses ateliers se le disputent-ils avec ceux de la ville ? Tous les ateliers en consomment-ils autant ?
- **Pollution** : est-elle propre à chaque ville, ou s'étend-elle à toute une région ? Peut-on la réduire ou s'en protéger ?
- **Sièges** : comment se résout un siège (combat simulé, comparaison de forces) ? Le joueur peut-il y prendre part, par exemple en ravitaillant la ville assiégée ou en envoyant des navires armés ?
- **Chute d'une ville** : que deviennent les ateliers, les marchandises et les navires du joueur dans une ville qui tombe ? Combien de temps dure la reconstruction, et la ville repart-elle de zéro ? Que se passe-t-il si c'est la ville natale du joueur, ou celle dont il est maire ?
- **Exploration** : la carte est-elle cachée au départ ? Combien de cités au total ?
- **Combat naval** : en temps réel, au tour par tour, ou résolu automatiquement ?
- **Réputation** : une réputation par ville, ou une seule ? Qu'est-ce qui la fait monter ou baisser ?
- **Gouvernement** : comment devient-on maire, si ce n'est pas par une élection ? Peut-on perdre sa charge ?
- **Reconquête** : comment se déroule la conquête des terres (armées sur la carte, batailles) ? Quel lien entre une colonie et le commerce maritime ?
- **Fin de partie** : comment gagne-t-on, et comment perd-on ?
