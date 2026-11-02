# Résultats provisoires — 1 novembre 2026

Suite autorisée : voir les [résultats sans ombre](RESULTS-NO-SHADOW.md) et la [proposition d'intégration au principal](INTEGRATION.md). Les constats ci-dessous conservent le périmètre de la première campagne avec ombre.

**Aucun framework n'est retenu.** WPF/WebView2 en composition donne une piste réelle pour le passage par alpha zéro, mais le rendu essayé échoue sur l'ombre décorative. Le popup séparé ne supprime pas cette limite. La recette complète du protocole n'est pas exécutée.

Tous les ajouts sont dans `spikes/window-feasibility/`. Aucun fichier métier, solution ou projet MAUI existant n'a été modifié. L'état initial Git était propre au commit `9c2bf9c2b69b0eaed1cef5c6210d3eee46ded55e`. Aucun commit, publication ou fusion effectué.

## Pile réellement exécutée

- Hôte **WPF distinct**, cible `net10.0-windows10.0.19041.0`, compilé avec SDK **10.0.402**, exécuté sous **.NET 10.0.13 x64**.
- SDK NuGet **Microsoft.Web.WebView2 1.0.3179.45**, runtime Edge/WebView2 chargé **156.0.4314.50**. Windows **10.0.26200.0** selon l'API ; cette chaîne ne suffit pas à nommer l'édition commerciale.
- Fenêtre WPF sans bordure, `AllowsTransparency=true`, fond transparent, topmost, `ShowActivated=false` au lancement. Le style Win32 journalisé contient `WS_EX_LAYERED` ; aucun `WS_EX_TRANSPARENT`, `SetWindowRgn`, masque de clic ou `HTTRANSPARENT` ajouté.
- Piste principale : `WebView2CompositionControl`, rendu web composé dans une image WPF. Le contrôle Microsoft utilise lui-même `GraphicsCaptureSession` ; notre code ne capture ni ne reconstruit de masque alpha. Ce mécanisme interne a un coût de capture et peut réduire le framerate, selon la [documentation Microsoft](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.wpf.webview2compositioncontrol). Ce compromis devra être discuté avant toute retenue ; aucune performance chiffrée n'est qualifiée.
- Comparaison : contrôle WebView2 WPF classique à HWND dans la même fenêtre layered. Ce résultat n'est pas une qualification de MAUI, WinUI, Electron ou Tauri.
- Rendu commun : petit **HTML/CSS/JavaScript**, pas encore Blazor. Barre 960×66, fenêtre unique 960×500, menu 320×260. En B, le menu occupe une seconde fenêtre propriétaire non modale. Le déplacement/DPI ferme le popup par conception du banc.

## Ce qui est observé

| Essai | Résultat limité | Preuve |
|---|---|---|
| T0, témoin indépendant sans barre | Clic, double-clic avec sélection d'un mot, clic droit/menu et molette avec défilement réel reçus. | `witness-composition-66060.jsonl`, 02:42:34–46 UTC. |
| A composition, P1 menu fermé | **Un essai par geste** : clic, double-clic/sélection du mot « pour », clic droit/menu du témoin, molette -360 puis défilement effectif. Le témoin reste derrière le prototype topmost. | Même témoin, 03:01:37–03:02:19 UTC, coordonnées physiques `(1320,570)` ; prototype `single-composition-50500.jsonl`. |
| A composition, alpha zéro | `WindowFromPoint` désigne le témoin en P1/P2/P3/P4/P7/P8, et P6 lorsque le menu est ouvert. C1/C2 désignent le prototype. | F8 dans `single-composition-50500.jsonl` et première passe `68124`. **Ciblage natif seulement**, pas les quatre gestes à chaque point. |
| A composition, ombre P5 | **Échec d'entrée** : le clic est reçu par `HTML`, ferme le menu et n'atteint pas le témoin ; la molette est reçue par `HTML`. `pointer-events:none` sur l'ombre ne change pas ce routage. Reproduit après relance à froid. | `68124`, 02:49:50/54 ; `50500`, 03:01:01/09 UTC, point DOM `(160,64)` / écran `(420,366)`. |
| B composition, popup séparé | Barre native 1440×99 et popup 480×390 physiques à 144 DPI. Ancre observée `(630,387)` pour origine de barre `(180,270)` : correspond à `(300,78)` DIP. Menu intégralement contenu dans 320×260. P6 cible le témoin, C2 le popup. | `split-composition-48764.jsonl`, `popup-placed`, `menu-state`, F8 fermé/ouvert. Pas une qualification des bords, déplacements ou gestes du popup. |
| B composition, ombre P5 | **Échec d'entrée**, même ombre ciblée par la barre et clic reçu par `HTML`. Réduire la taille native ne règle pas les pixels semi-transparents restants. | `split-composition-46940.jsonl` et relevés de `48764`. |
| WebView2 classique à HWND, A | Le rendu est visible, mais Windows désigne le témoin même au centre du bouton C1. Un vrai clic en `(318,318)` atteint le témoin et le compteur web reste à zéro. **Échec de l'interactivité de ce montage.** | `single-hwnd-57572.jsonl`, relevé F8 ; témoin `66060`, 02:55:00 UTC. |
| Contrôles web A composition | Compteur, action du menu, case, saisie, Tab et liste défilante fonctionnent ponctuellement ; clic droit dans le champ ouvre le menu Chromium. | `single-composition-67980.jsonl`, 02:46:34–02:47:02 UTC et observations visuelles. Pas de mesure de latence ; double-clic web non qualifié. |
| Échap sans menu contextuel ouvert | Le menu disparaît et `focusin` revient au déclencheur. Observation répétée à 100 % ; indicateur de focus visible. | `68124` puis `47300` ; événements `escape`, `menu-state`, `focusin menu-trigger`. |
| Écrans statiques 100 % / 150 % | Viewport stabilisé 960×500, DPR1 à 96 DPI et DPR1,5 à 144 DPI. Bouton et ouverture du menu observés à 100 %. Le relevé initial 640×334 à 100 % était transitoire. | `single-composition-47300.jsonl` : F8 `layout` actuel ; `50500` à 150 %. Aucun succès de transition DPI déduit. |

Les traces du témoin constituent une preuve entre processus pour les gestes cités. Les seules absences de lignes dans ce témoin ne servent pas de preuve positive. Les captures de l'outil ont été examinées dans la conversation ; aucune vidéo continue ni capture annotée exportée n'est fournie.

## Verdict et limites

**NO-GO du rendu A/B essayé pour l'exigence stricte « ombre décorative traversable ».** L'alpha zéro et l'alpha faible ne sont pas équivalents pour cette fenêtre layered. Le succès limité en P1 n'annule pas l'échec en P5. Ce verdict ne signifie pas que tout rendu web organique est impossible.

**NO-GO du montage WPF layered + WebView2 classique testé**, puisque le contrôle visible laisse fuir un clic vers le témoin. Ne pas extrapoler à tous les hôtes .NET.

**NON CONCLUANT pour une solution produit complète.** Restent notamment :

- 10 répétitions des quatre gestes à tous les points, deux applications témoins usuelles et états actif/inactif ; gestes traversants menu ouvert ; double-clic web fiable ; clic extérieur transmis au premier geste.
- Absence de vol de focus au lancement et pendant 60 secondes, Alt+Tab, clavier complet et contexte Chromium/Échap. L'outil CUA active sa fenêtre cible avant les entrées et refuse les clics visant une autre fenêtre : il ne permet pas de qualifier ces cas sans perturber le phénomène.
- Contrôles, clavier, cycles rapides et clic extérieur du popup dans une campagne manuelle. Le popup propriétaire n'était pas exposé comme fenêtre cible séparée par l'inventaire CUA.
- Déplacement effectif : la tentative par poignée a fermé le popup, mais aucun changement de rectangle natif n'a été enregistré. Aucun succès de drag n'est revendiqué ; le pont JS→`DragMove` doit être essayé manuellement.
- Passage entre écrans, DPI à chaud, 125 % / 200 %, jonctions/bords, ancrage à 2 pixels après mouvement, déconnexion/reconnexion et veille. Quatre écrans existent (deux à 96 DPI, deux à 144 DPI) ; seul un relevé statique à 100 %/150 % a été fait. Une première fenêtre partiellement hors écran (`65736`) a été exclue puis relancée entièrement visible.
- Variation des polices/formes/contenus, restauration dans un dossier neuf, mise à jour runtime, intégration Blazor et autres OS.

Le code du banc n'utilise ni polling du curseur, hook global, bascule sur survol, réinjection de gestes, reconstruction de masque alpha ni géométrie native d'entrée dupliquée. F8 effectue uniquement des requêtes ponctuelles aux points fixes de l'atlas. Les journaux synchrones peuvent perturber une mesure fine de réactivité.

Electron et Tauri ont été comparés par leurs **documentations officielles**, pas installés ni lancés. Electron documente explicitement que ses zones transparentes ne sont pas automatiquement traversables. Tauri expose une ignorance globale du curseur, sans garantie de sélection automatique par alpha. Voir les sources et distinctions dans le [guide](README.md#limites-documentaires). L'absence d'essai Tauri reste « non vérifié », pas un échec expérimental.

## Reproduire et examiner

```powershell
pwsh ./spikes/window-feasibility/build.ps1
pwsh ./spikes/window-feasibility/run.ps1 -Mode witness
pwsh ./spikes/window-feasibility/run.ps1 -Mode single
# Fermer single, puis :
pwsh ./spikes/window-feasibility/run.ps1 -Mode split
# Comparaison de contrôle :
pwsh ./spikes/window-feasibility/run.ps1 -Mode single -Renderer hwnd
```

F8 relève les cibles natives et le viewport actuel ; ce n'est pas un test de clic. Journaux par défaut : `%TEMP%\DevToolbar-feasibility`. Fermer avec le bouton ou `Alt+F4`. Les profils WebView2 temporaires et les sorties de compilation locales restent disponibles.

La compilation finale Release et la restauration verrouillée hors ligne ont réussi avec **0 avertissement, 0 erreur**. Aucun test de la solution métier n'a été nécessaire : elle n'a pas changé et ses E2E navigateur ne mesurent pas ce routage natif. Vérifications complémentaires : syntaxe JS des trois rôles, JSON atlas et lockfile, syntaxe PowerShell, contrôle des fichiers ajoutés et des preuves.

Consulter [observations.csv](evidence/2026-11-01/observations.csv), le [registre des preuves](evidence/2026-11-01/README.md) et les JSONL bruts. `final-build-manifest.json` contient les hashes du kit final, dont `Probe.dll` (le hash de l'apphost `.exe` seul ne décrit pas le code géré). Les premières itérations ne sont pas toutes accompagnées de leur hash DLL : elles sont conservées comme diagnostic, pas comme campagne entièrement figée. Les passes finales A `50500` et 100 % `47300` utilisent le kit final.

Le premier restore a rencontré une configuration NuGet personnelle inaccessible ; le script isole `APPDATA` pour la compilation sans lire cette configuration. La compilation partagée a aussi été désactivée après un refus d'écriture par le serveur de compilation. Les premiers lancements ont révélé puis corrigé une dépendance Windows SDK manquante, une navigation locale `data:` annulée et des défauts de focus du harnais. Ces incidents ne sont pas des échecs des gestes du framework. Les processus lancés dans le bac à sable n'étaient pas visibles par CUA ; les essais cités ont ensuite été lancés sur le bureau interactif. Tous les processus du banc sont fermés en fin de session.

**Décisions à demander à Benoit avant la suite :** faut-il conserver l'exigence d'une ombre web extérieure traversable et poursuivre la comparaison expérimentale Electron/Tauri, ou accepter d'étudier une variante visuelle sans cette ombre/avec une ombre native ? Aucun de ces changements de périmètre n'a été appliqué. L'installation locale d'Electron/Rust/Tauri reste en attente de son choix.
