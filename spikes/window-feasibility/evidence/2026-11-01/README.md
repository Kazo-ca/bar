# Observations Windows du 1 novembre 2026

Ce dossier contient des journaux JSONL de prototypes isolés et un relevé d'observations, pas une campagne de recette complète. Les heures des journaux et du CSV sont en **UTC**. Aucune capture vidéo n'a été produite.

`observations.csv` contient une ligne par observation précise, avec fichier et numéro de ligne de preuve. Plusieurs points lus lors du même appel F8 produisent plusieurs lignes : leur nombre ne représente pas un nombre de gestes ni de tests réussis.

## Lire les statuts

| Statut | Sens |
|---|---|
| `OBSERVED_ROUTING` | Résultat ponctuel de `WindowFromPoint`, sans injection d'entrée. Identifie la fenêtre ciblée à une coordonnée ; ne prouve ni clic, ni double-clic, ni clic droit, ni défilement reçu. |
| `OBSERVED_INPUT_FAILURE` | Une entrée réelle et son destinataire constaté contredisent le comportement attendu au point indiqué. La conclusion reste limitée à cette construction et à ce geste. |
| `OBSERVED_INPUT_SUCCESS` | Une entrée réelle est reçue avec l'effet attendu au point et dans l'état indiqués. Une ligne correspond ici à **un seul essai du geste**, sans extrapolation à la matrice complète. |
| `BASELINE` | Vérification limitée du témoin, d'un contrôle, d'un état ou d'une disposition effectivement journalisés. Ce statut ne signifie pas que la transparence traversante est validée. |
| `NOT_TESTED` | Vérification manquante, point exclu ou essai non qualifiant. Le motif figure dans `notes`. |

Les coordonnées des échantillons `WindowFromPoint` et du témoin natif viennent des journaux en pixels physiques. Pour les contrôles DOM à 144 DPI, les coordonnées écran renseignées sont calculées à partir des coordonnées CSS et de l'origine native, avec facteur 1,5. Les événements DOM ne journalisent pas eux-mêmes une position écran physique. Les PID identifient des processus de cette session, sans garantie de réutilisation lors d'un prochain lancement.

## Environnement effectivement relevé

- Windows `10.0.26200.0`, processus x64, .NET **10.0.13** ; le snapshot ne relève pas le numéro UBR complet.
- Runtime WebView2 réellement chargé : **156.0.4314.50**. Le projet verrouille le SDK NuGet **1.0.3179.45** ; ces deux versions ont des rôles distincts.
- Quatre écrans énumérés : DISPLAY1 `(-1903,-1080)-(17,0)` à 96 DPI ; DISPLAY2 primaire `(0,0)-(3840,2160)` à 144 DPI ; DISPLAY3 `(17,-1620)-(2897,0)` à 144 DPI ; DISPLAY4 `(-1920,0)-(0,1080)` à 96 DPI.
- Routage de molette Windows : valeur **2**, vers la position souris ; trois lignes de défilement ; délai de double-clic **500 ms** ; session distante signalée `false`.
- Aucun snapshot ne prouve à lui seul un déplacement, un changement de DPI correctement géré ou une recette sur tous les écrans.

## Journaux retenus et portée

| Journal | Observation et limite |
|---|---|
| [witness-composition-66060.jsonl](witness-composition-66060.jsonl) | Témoin dans un processus distinct. À **02:42:34–46**, au point écran `(369,360)` : clics reçus, deuxième clic `clickCount=2` puis sélection de longueur 8, bouton droit reçu, molette `-360` et défilement effectif de 48. À **02:55:00**, réception du clic destiné au contrôle C1 du prototype HWND, au point `(318,318)`. |
| [single-composition-67980.jsonl](single-composition-67980.jsonl) | Contrôles web : compteur, action, case, saisie et Tab, liste défilante, événement de menu contextuel. Aucun `dblclick` DOM n'est enregistré ; un `click detail=3` et des `pointerdown` répétés ne remplacent pas une vérification du double-clic. Les valeurs finales des contrôles ne sont pas échantillonnées dans cette version. |
| [single-composition-68124.jsonl](single-composition-68124.jsonl) | A, fenêtre unique, écran primaire à 144 DPI. F8 fermé puis ouvert : P1/P2/P3/P4/P7/P8 ciblent le témoin ; P6 le cible lorsque le menu est ouvert ; P5 cible le prototype ; C1/C2 ciblent le prototype lorsqu'applicables. **À 02:49:50, un clic réel en P5 arrive au DOM `HTML` et ferme le menu ; à 02:49:54, la molette arrive aussi au DOM `HTML`.** Aucun événement témoin correspondant n'est journalisé. L'ombre décorative hors silhouette absorbe ces deux gestes. Une séquence Échap ultérieure ferme le menu et rend le focus au déclencheur. |
| [split-composition-46940.jsonl](split-composition-46940.jsonl) | Premier B : popup visible puis fermeture liée à un défaut du harnais pendant une transition de focus. Ce comportement de focus est exclu du verdict framework. Séparément, à **02:52:03**, menu déjà fermé, le clic réel P5 arrive au DOM `HTML` de la barre ; la réduction de la fenêtre ne résout donc pas ce cas d'ombre. |
| [split-composition-48764.jsonl](split-composition-48764.jsonl) | B après correction du défaut de transition de focus. Barre native 1440×99 pixels, popup 480×390 à `(630,387)`. F8 fermé puis ouvert ; mêmes destinataires par point que A, avec C2 ciblant la fenêtre popup lorsque le menu est ouvert. La popup et le focus `menu-action` sont constatés ; l'activation ultérieure de la barre conserve le menu pour le relevé F8. La recette complète des contrôles et fermetures du popup reste à faire. |
| [single-hwnd-57572.jsonl](single-hwnd-57572.jsonl) | Contrôle avec WebView2 WPF classique à HWND dans la même construction layered. F8 cible le témoin aussi en C1. À **02:55:00**, le clic réel écran `(318,318)` est reçu par le témoin et aucun clic DOM n'est journalisé ; l'opérateur constate le compteur restant à 0. Le journal ne contient que l'état initial `count=0`, sans nouvel échantillon du compteur après le clic. Cela qualifie l'échec de cette construction, pas l'ensemble de WebView2 ou de MAUI. |
| [single-composition-65736.jsonl](single-composition-65736.jsonl) | Premier relevé à 96 DPI, fenêtre `(-2100,300)-(-1140,800)` partiellement hors écran. **P2, P5, P8 et C1 sont hors de tous les moniteurs énumérés : exclus**, menus ouvert et fermé. Leurs retours bruts restent conservés et sont marqués `NOT_TESTED`. Les autres lignes ne sont que des cibles physiques ponctuelles. |
| [single-composition-51776.jsonl](single-composition-51776.jsonl) | Reprise à 96 DPI avec fenêtre entièrement visible `(-1350,300)-(-390,800)` sur DISPLAY4. F8 fermé puis ouvert : P1/P2/P3/P4/P6/P7/P8 vers PID54160, dont l'application n'est pas identifiée dans les journaux ; P5/C1/C2 vers le prototype lorsqu'applicables. Clic DOM C1 puis état `count=1` constatés. Le `ready` initial annonce toutefois un viewport 640×334, DPR1, pour une fenêtre native 960×500 ; sans viewport actuel dans cette version, la cohérence CSS/DIP au moment du relevé reste **non qualifiée**. |
| [single-composition-47300.jsonl](single-composition-47300.jsonl) | Reprise instrumentée sur DISPLAY4, même position entièrement visible à 96 DPI. Le viewport initial 640×334 devient **960×500, DPR1**, confirmé par les événements `layout` à **02:59:11**, au F8 fermé **02:59:48**, puis au F8 ouvert **03:00:02**. Le caractère transitoire du viewport initial est donc observé dans cette reprise. Destinataires F8 identiques à 51776. Échap à **03:00:03** ferme le menu et remet le focus sur `menu-trigger`, avec viewport courant 960×500. Cela reste un lancement statique à 96 DPI, sans qualification des transitions entre écrans. |

P5 est défini comme une ombre décorative hors silhouette, attendue traversable même lorsque son alpha n'est pas nul. Il ne faut pas confondre son échec avec le comportement des pixels d'alpha exactement zéro. Le relevé favorable des cibles aux autres points n'est pas une mesure de livraison de leurs entrées.

## Reprise finale avec entrées réellement reçues en P1

[single-composition-50500.jsonl](single-composition-50500.jsonl) correspond au lancement à froid du kit final, variante A sur l'écran primaire à 144 DPI, viewport 960×500 et DPR1,5. Les relevés F8 fermé à **03:00:50** puis ouvert à **03:00:55** retrouvent les mêmes cibles que 68124. Le clic réel P5 à **03:01:01** et la molette P5 à **03:01:09** sont de nouveau absorbés par le DOM `HTML` ; le clic ferme le menu. Ces deux gestes reproduisent l'échec de l'ombre décorative.

La suite utilise le témoin natif au point physique **P1 `(1320,570)`**, dans la grande zone vide de la fenêtre A, **menu fermé**. L'opérateur constate que le prototype reste affiché et topmost. Quatre gestes sont réellement exécutés et reçus par le processus témoin 66060 :

| Geste, un essai chacun | Preuve UTC dans le témoin |
|---|---|
| Clic simple | **03:01:37**, lignes 54–56 : bouton gauche down/up, `clickCount=1`, sélection modifiée. |
| Double-clic | **03:01:43**, lignes 57–62 : deuxième down `clickCount=2`, sélection de longueur 5 ; l'opérateur voit le mot « pour » sélectionné. |
| Clic droit | **03:01:50**, lignes 63–64 : bouton droit down/up ; menu contextuel standard visible constaté par l'opérateur. La visibilité du menu n'est pas encodée séparément dans le JSONL. |
| Molette et effet | **03:02:19**, lignes 66–67 : `delta=-360`, puis `verticalChange=48` et `verticalOffset=48`. |

**Limite de ces quatre succès : l'outil CUA cible et active le témoin avant chaque geste.** Ils prouvent la réception de ces entrées en P1 dans cette configuration visible, mais pas la conservation du focus, le premier clic d'une fenêtre inactive ni la fermeture au clic extérieur. Il ne s'agit pas de dix répétitions, ni d'une recette de P2–P8 ou du menu ouvert. Le CSV contient exactement quatre lignes `OBSERVED_INPUT_SUCCESS`, une par geste.

## Essais du harnais exclus

- [single-composition-34744.jsonl](single-composition-34744.jsonl) : assembly `Microsoft.Windows.SDK.NET` manquant. Défaut de dépendance du harnais corrigé avant les reprises.
- [single-composition-66840.jsonl](single-composition-66840.jsonl) : navigation échouée, `WebErrorStatus=14`, diagnostic de garde du harnais annulant l'URI `data:`. Corrigé avant les reprises ; une fenêtre sans contenu ne démontre pas la faisabilité.
- La première version interceptait Échap dans `PreviewKeyDown` WPF, perturbant le clavier du menu contextuel de la saisie. Les observations initiales concernées ne qualifient pas ce scénario ; la fermeture est ensuite confiée au DOM.
- [native-composition-18880.jsonl](native-composition-18880.jsonl) : gestes présents dans le témoin pendant une période où l'outil d'interaction signalait une interruption par entrée utilisateur. Leur provenance n'est pas établie ; essai natif **non qualifiant**. Aucun geste non piloté n'est attribué à l'automatisation.
- Les journaux `witness-composition-57180.jsonl` et `witness-composition-66312.jsonl` ne constituent pas des campagnes d'entrée ; ils sont conservés comme traces de lancement.

## Vérifications restant nécessaires

Il reste à compléter la matrice de clics simples, doubles et droits, ainsi que la molette et le défilement effectif, à chaque point transparent et dans les deux états du menu. Les quatre gestes P1 fermé ci-dessus et les échecs P5 sont les mesures d'entrée précises consignées ; les échantillons F8 ne remplissent aucune case supplémentaire à eux seuls. Restent aussi les contrôles complets du popup, clic extérieur, focus et clavier, déplacement, changement de moniteur et de DPI, bords anticrénelés et réactivité mesurée.

Le journal écrit sur disque de façon synchrone à chaque événement : aucune mesure fine de latence n'est revendiquée. Ni Electron, ni Tauri, ni une intégration Blazor dans cette nouvelle coque, ni un autre OS n'ont été exécutés dans ce dossier de preuves.

Les JSONL bruts n'ont pas été réécrits pour produire ce relevé. Les défauts et exclusions sont signalés sans transformer une absence de mesure en réussite ou en échec framework.
