# Reprise sans ombre — preuves du 1 novembre 2026

Essai A seulement : fenêtre unique WPF/WebView2 en composition, `NoShadow=true`. Les traces concernent le prototype PID **27536** et le témoin indépendant PID **67304**. Les horodatages ci-dessous sont UTC.

Sources : [S — prototype](single-composition-27536.jsonl), [W — témoin](witness-composition-67304.jsonl), [manifeste](build-manifest.json), [observations.csv](observations.csv). `S:25` désigne la ligne 25 du JSONL prototype. Aucun JSONL brut n'a été réécrit.

| Preuve | Fait observé et limite |
|---|---|
| S:1–8, 03:17:27–29 | `NoShadow=true`, .NET 10.0.13 x64, WebView2 chargé 156.0.4314.50 ; fenêtre physique `(180,270)-(1620,1020)`, 144 DPI, viewport 960×500, DPR1,5. |
| S:10, 03:17:52.915587 | F8, menu fermé : P1/P2/P3/P4/P5/P7/P8 ciblent PID14864, dont l'application n'est pas identifiée dans les logs ; C1 cible le prototype. |
| S:14, 03:18:20.9760856 | F8, menu fermé : mêmes points vides vers le témoin PID67304, C1 vers le prototype. **P5 est l'ancien emplacement de l'ombre, maintenant vide.** |
| S:19–26, 03:18:21 | Clic sur le déclencheur, ouverture et focus DOM `menu-action`. F8 ouvert S:25 : P1–P8 vers le témoin ; C1/C2 vers le prototype. Ces relevés `WindowFromPoint` n'envoient aucun geste. |
| S:27–30 et W:7–15, 03:19:27–30 | Le prototype se désactive, ferme le menu avec `restoreTrigger=false` à **03:19:27.2039081** ; le témoin reçoit un clic gauche en `(764,343)` à **03:19:27.2102422**, puis les molettes `+120,-120,-120` en `(761,339)`. Les deux molettes négatives sont suivies de défilements de +48, offsets48 puis96. Au mouse-down enregistré, le menu est déjà fermé. |
| Retour utilisateur transmis par le parent | Benoit a répondu « tout fonctionne comme je le veux » au scénario précis : **un seul clic dans le texte visible à droite du menu ouvert, puis molette ; premier clic reçu, menu fermé, molette fonctionnelle**. Statut `MANUAL_REPORTED_SUCCESS` pour ce scénario seulement. Les traces précédentes sont compatibles avec ce retour ; elles ne prouvent pas seules le nombre d'intentions de clic ni sa provenance. Aucun horodatage propre du retour n'est disponible : `reportUtc` reste vide dans le CSV. |
| W:8–47, 03:19:27–56 | Dix paires gauche down/up, toutes `clickCount=1`, dix changements de sélection de longueur0. Trois événements molette et trois changements de scroll sont journalisés. Aucun clic droit, `clickCount=2`, clavier ou édition du texte dans cette plage du témoin. Les autres gestes ne sont attribués à aucune personne. |
| S:32–35, 03:19:37 ; S:207–209, 03:19:52 | Réception DOM d'un clic sur `counter`, puis d'un `contextmenu` sur ce contrôle. Pas de nouvel échantillon de valeur du compteur ni de preuve exportée du menu contextuel natif. |
| S:42–200 et 216–353, 03:19:40–43 puis 03:20:32–34 | **297 notifications natives `location`**, toutes à144DPI, avec changement effectif du rectangle ; dernier rectangle `(1462,273)-(2902,1023)`. Ce sont des notifications de position, pas une mesure de framerate. Pas de franchissement de DPI qualifié. |
| S:364–368, 03:20:41 | Clic DOM `quit`, puis `closed` et `exit`. La fermeture du prototype est journalisée ; pas de fermeture du témoin dans son instantané de48lignes. |

Les statuts du CSV séparent `OBSERVED_ROUTING` (cible native), `OBSERVED_INPUT` (événement/effet journalisé), `MANUAL_REPORTED_SUCCESS` (retour utilisateur sur un scénario) et `NOT_TESTED` (recette manquante). Aucun de ces statuts ne déclare la campagne complète réussie.

Restent notamment les dix répétitions de tous les gestes à chaque point, le double-clic et clic droit traversants, les entrées pendant que le menu reste ouvert, le clavier/Échap et les contrôles complets du menu, les cycles, l'absence de vol de focus, B avec popup séparé sans ombre, les changements de moniteur/DPI et 125%/200%. Le retour utilisateur ci-dessus n'est pas étendu à ces cas.

L'outil CUA active sa cible avant les entrées et refuse les points appartenant à une autre fenêtre ; ses limites demeurent pour les tests sensibles au focus. Le scénario confirmé manuellement est enregistré séparément des interactions CUA et des événements dont la provenance n'est pas établie.

Le manifeste déclare `Probe.dll` SHA-256 **73476C1BAE5CDE0EDEA0B84901C957F4F1628FA5564E08ABAE2D12BDB8851757**. L'agent principal a communiqué : compilation Release sans avertissement/erreur et validation JS de six cas. Cette rédaction n'a pas réexécuté ces vérifications ; elle a lu les traces et le manifeste.
