# DevToolbar — travail en pause

Pause demandée par Benoit le **1 novembre 2026**. Ce document accompagne le commit de sauvegarde de la branche **`codex/pause-window-feasibility-2026-11-01`**, créée depuis `main` au commit `9c2bf9c2b69b0eaed1cef5c6210d3eee46ded55e`.

**Ne pas commencer le changement d'hôte en attente de décision. Aucun push, fusion ou déploiement n'est autorisé par cette sauvegarde.** Attendre une instruction de reprise.

## Ce qui était en cours

Déterminer comment afficher une barre de bureau à rendu web, transparente et organique, avec un menu débordant, tout en laissant les applications derrière recevoir les entrées dans les zones vides. Critères : contrôles utilisables, clavier/focus cohérents, sans surveillance permanente du curseur, masque manuel fragile ni infrastructure disproportionnée.

Après les essais, Benoit a demandé l'application au projet principal. L'audit a identifié un choix d'architecture significatif : ajouter un hôte WPF Windows parallèle ou conserver impérativement l'hôte MAUI/WinUI. **Ce choix n'a pas été confirmé.** Aucune intégration au principal n'a été commencée.

## Travail sauvegardé

- Banc isolé dans ce dossier : WPF, .NET 10, WebView2, rendu HTML/CSS/JS partagé. Modes `single`, `split`, `native`, `opaque`, `witness` ; comparaison des contrôles WebView2 `composition` et `hwnd`.
- Option `-NoShadow` / `--no-shadow`, ajoutée après accord de Benoit. Elle masque uniquement l'ombre extérieure du banc ; la variante avec ombre reste disponible par défaut.
- Témoin natif dans un processus indépendant, journaux des événements réellement reçus, relevés ponctuels F8 des cibles Win32 et du viewport. Aucun masque d'entrée, polling souris ou réinjection de gestes dans le prototype.
- Sources, scripts de build/lancement, verrouillage NuGet, atlas, résultats, CSV et JSONL des deux campagnes conservés. Les sorties `bin/obj` et profils WebView2 temporaires ne sont pas versionnés.
- Audit de l'architecture principale et proposition d'intégration dans [INTEGRATION.md](INTEGRATION.md). Aucun fichier métier, projet existant ou solution n'a été modifié avant cette sauvegarde.

Le dépôt était propre au début de la session. Au contrôle de pause, seuls les fichiers de ce dossier étaient non suivis ; aucun changement suivi préexistant ni index préparé par l'utilisateur n'a été écrasé ou inclus.

## Résultats et tests déjà effectués

Pile exécutée : SDK **.NET 10.0.402**, runtime **10.0.13 x64**, SDK WebView2 **1.0.3179.45**, runtime WebView2 chargé **156.0.4314.50**, Windows API version **10.0.26200.0**. Piste principale : fenêtre WPF layered via `AllowsTransparency`, avec `WebView2CompositionControl`. Le banc n'est pas l'application MAUI/Blazor originale.

| Résultat | Portée exacte |
|---|---|
| Compilation et contrôles statiques | Build Release du banc : 0 avertissement, 0 erreur. Syntaxe PowerShell, JavaScript des trois rôles avec/sans ombre, JSON et CSV vérifiés. |
| Entrées traversantes en P1, avec ombre | Un essai de chacun des quatre gestes, menu fermé : clic, double-clic avec sélection de mot, clic droit avec menu du témoin et molette avec défilement effectif. CUA active le témoin avant le geste : pas une preuve du focus ou du premier clic d'une fenêtre inactive. |
| Échec de l'ombre | En A composition, clic et molette en P5 reçus par le DOM au lieu du témoin ; reproduit à froid. La barre compacte conserve aussi ce problème d'ombre. |
| Contrôle classique à HWND | Dans le montage WPF layered essayé, un clic sur un bouton visible atteint le témoin ; ce montage est inutilisable. Ne pas extrapoler à tous les hôtes .NET. |
| Sans ombre | F8 désigne le témoin aux points transparents, y compris l'ancien emplacement de l'ombre P5, menu fermé et ouvert. F8 mesure une cible, pas la livraison de gestes. |
| Retour manuel de Benoit | Pour **un clic extérieur dans le texte visible depuis le menu ouvert, puis molette**, Benoit a déclaré « tout fonctionne comme je le veux ». Consigné comme `MANUAL_REPORTED_SUCCESS` pour ce seul scénario, concordant avec les traces clic reçu/fermeture sans reprise forcée du focus, puis défilement. |
| Déplacement | La reprise sans ombre contient deux déplacements réels et 297 positions natives, tous à 144 DPI. Aucune transition d'écran ou de DPI n'est démontrée. |
| DPI statiques | Première campagne : viewport stabilisé 960×500 à 96 DPI/DPR1 et à 144 DPI/DPR1,5. Cela ne qualifie pas les changements d'échelle à chaud. |
| Principal MAUI | Restore Windows hors ligne tenté ; échec pour paquets absents du cache. Compilation MAUI non exécutée, aucune conclusion d'erreur de code. Détail dans INTEGRATION.md. |

Les bilans complets distinguent preuves, observations visuelles, retour utilisateur, incidents du harnais et exclusions : [avec ombre](RESULTS.md), [sans ombre](RESULTS-NO-SHADOW.md), [preuves initiales](evidence/2026-11-01/README.md), [preuves sans ombre](evidence/2026-11-01-no-shadow/README.md).

## Limites et décisions en attente

- **Pas de GO global.** Restent la matrice complète de gestes/répétitions/points, menu maintenu ouvert pendant la molette, focus, clavier/contexte Chromium, popup sans ombre, plusieurs applications témoins, transitions DPI et écrans, bords, veille et performances.
- Le retour favorable de Benoit ne vaut pas validation de ces cas ni accord sur un changement d'hôte.
- L'hôte courant est MAUI/WinUI ; la piste validée est WPF. Le `BlazorWebView` WPF officiel **10.0.110** utilise déjà le contrôle de composition, donc Blazor Hybrid pourrait être conservé sans pont personnalisé. L'ajout d'un hôte WPF parallèle réutilisant UI/Core/Plugins est proposé, **pas décidé**.
- Les paquets BlazorWebView WPF/base ne sont pas dans le cache local examiné. Les acquérir sera nécessaire si cette voie est autorisée. Ne pas masquer les dépendances manquantes par un changement silencieux de versions.
- Electron et Tauri : documentation officielle comparée, aucune installation ni exécution. Leur comparaison expérimentale n'est pas autorisée à ce stade.
- Computer Use a été arrêté par Échap physique lors de la tentative de fermeture du témoin. Aucun autre geste UI n'a été envoyé après cet arrêt. Lors de la pause, le seul processus témoin restant, PID 67304, a été arrêté après vérification de son chemin dans ce dépôt ; **aucun processus Probe ne reste actif**. L'arrêt forcé explique l'absence éventuelle de ligne `exit` finale de ce témoin.

## Reprise, uniquement après nouvelle instruction

1. Vérifier la branche, le commit de pause, `git status` et les consignes nouvelles ; préserver tout changement ajouté entretemps.
2. Lire les bilans et INTEGRATION.md. Obtenir le choix d'hôte Windows avant d'implémenter l'intégration. Ne pas remplacer MAUI implicitement.
3. Si le banc doit être rejoué, utiliser les commandes ci-dessous ; un build hors ligne exige les dépendances en cache. `-Online` n'est à utiliser qu'en connaissance des paquets à restaurer.
4. Compléter les scénarios pertinents manuellement lorsque CUA perturbe l'activation ; consigner séparément le retour utilisateur et les traces.
5. Après accord d'architecture seulement : intégrer le rendu Blazor, les services natifs et la fermeture des menus, traiter les ombres et overlays réels, puis compiler et vérifier les régressions concernées.

```powershell
pwsh ./spikes/window-feasibility/build.ps1
pwsh ./spikes/window-feasibility/run.ps1 -Mode witness
pwsh ./spikes/window-feasibility/run.ps1 -Mode single -NoShadow
# Après fermeture de single, pour une future recette :
pwsh ./spikes/window-feasibility/run.ps1 -Mode split -NoShadow
```

Journaux/profils par défaut : `%TEMP%\DevToolbar-feasibility`. Les preuves versionnées restent dans `evidence/`. Le protocole de référence original se trouve sur ce PC à `C:\Users\clefw\Documents\Codex\2026-10-31\task\protocole-faisabilite-dev-toolbar.md` ; il décrivait un plan, pas des résultats déjà démontrés.

Cette pause sauvegarde le travail local sur une branche. Aucun push ni fusion n'a été effectué.
