# Faisabilité des fenêtres de DevToolbar sous Windows

**Travail en pause à la demande de Benoit.** Lire [RESUME.md](RESUME.md) pour l'état sauvegardé, les limites et les décisions nécessaires avant reprise. Le changement d'hôte reste en attente.

Ce spike compare une fenêtre web transparente unique et une barre compacte avec
popup web séparé. La question déterminante est le passage des entrées aux autres
applications dans les zones vides, tout en gardant les contrôles utilisables.
Voir le bureau au travers ne suffit pas à répondre.

Le projet WPF est isolé : il ne modifie ni les solutions, ni l'hôte MAUI, ni les
outils métier. Le rendu est du HTML/CSS/JavaScript commun aux essais, **pas encore
du Blazor**. Il ne constitue pas une décision de migration. Les résultats d'une
campagne sont consignés séparément dans [RESULTS.md](RESULTS.md) ; ce guide
n'annonce aucune réussite empirique.

La nouvelle vague autorisée porte sur le même prototype WPF **sans ombre**.
Ses résultats seront consignés dans [RESULTS-NO-SHADOW.md](RESULTS-NO-SHADOW.md),
sans remplacer les résultats et preuves précédents. Electron et Tauri restent
des comparaisons documentaires ; aucun essai de ces runtimes n'est inclus.

## Construire et lancer

Prérequis : Windows, SDK .NET 10 et runtime Microsoft Edge WebView2 installé.
La cible est `net10.0-windows10.0.19041.0`, avec
`WindowsSdkPackageVersion=10.0.19041.57`. Le paquet NuGet `Microsoft.Web.WebView2`
est fixé exactement à `1.0.3179.45`, avec
`packages.lock.json`. La version du runtime WebView2 réellement chargé est
indépendante du paquet SDK et apparaît dans le journal.

Depuis la racine du dépôt :

```powershell
# Restore verrouillé et build Release, hors ligne depuis le cache local.
pwsh ./spikes/window-feasibility/build.ps1

# Seulement si les paquets manquent : autoriser explicitement nuget.org.
pwsh ./spikes/window-feasibility/build.ps1 -Online

pwsh ./spikes/window-feasibility/run.ps1 -Mode witness
```

Laisser le témoin ouvert et lancer le prototype dans un autre terminal :

```powershell
pwsh ./spikes/window-feasibility/run.ps1 -Mode single
```

Fermer le prototype avant de changer de variante, puis recommencer avec `split`.
Le témoin doit être un **autre processus**, sous le prototype. Les boutons
« Fermer » et `Alt+F4` permettent de quitter ; Échap sert au menu.

Le build utilise `dotnet restore --locked-mode`, sa configuration NuGet locale et,
par défaut, `NUGET_PACKAGES` ou `%USERPROFILE%\.nuget\packages`. Un paquet absent
du cache est une dépendance manquante, pas un résultat sur les fenêtres. Le build
produit aussi `build-manifest.json` avec les SHA-256. Fermer les processus du
spike avant de reconstruire.

Le script de lancement accepte `-Mode single|split|native|opaque|witness`,
`-Renderer composition|hwnd` (défaut `composition`), `-Left`, `-Top`,
`-LogDirectory` et le commutateur `-NoShadow`. Ces options correspondent à
`--mode`, `--renderer`, `--left`, `--top`, `--log-dir`, `--no-shadow` du binaire.
Les positions sont en unités logiques WPF ; des
valeurs négatives sont possibles sur plusieurs écrans. Le témoin conserve sa
position propre ; le placer sous les points à tester si nécessaire.

```powershell
pwsh ./spikes/window-feasibility/run.ps1 -Mode split -Renderer composition -Left 120 -Top 180 -LogDirectory "$env:TEMP/DevToolbar-feasibility/campaign-01"
```

Pour la **nouvelle vague sans ombre**, reconstruire puis utiliser un nouveau
dossier de journaux. Lancer `single` et `split` successivement, en conservant le
témoin ouvert :

```powershell
pwsh ./spikes/window-feasibility/build.ps1
pwsh ./spikes/window-feasibility/run.ps1 -Mode witness -LogDirectory "$env:TEMP/DevToolbar-feasibility/no-shadow-01"
pwsh ./spikes/window-feasibility/run.ps1 -Mode single -Renderer composition -NoShadow -LogDirectory "$env:TEMP/DevToolbar-feasibility/no-shadow-01"
# Fermer single avant ce lancement.
pwsh ./spikes/window-feasibility/run.ps1 -Mode split -Renderer composition -NoShadow -LogDirectory "$env:TEMP/DevToolbar-feasibility/no-shadow-01"
```

Sans le commutateur, le scénario avec ombre reste le défaut pour reproduire la
campagne initiale. `-NoShadow` masque uniquement l'élément CSS `#shadow` ; il ne
modifie ni les régions natives ni la géométrie des contrôles. L'option est
propagée au popup et inscrite dans les options et états web journalisés sous
`NoShadow`/`noShadow`. Conserver P5 aux mêmes coordonnées : il désigne alors la
zone où l'ombre a été retirée. Refaire la recette ci-dessous, menu fermé et
ouvert, sans déduire une conformité de la seule disparition visuelle de l'ombre.

## Variantes et contrôles

| Mode | Fenêtres et objectif |
|---|---|
| `single` | Une fenêtre de 960 × 500 unités logiques ; barre visible de 960 × 66 et menu web de 320 × 260 dans la même fenêtre. Test du grand rectangle transparent. |
| `split` | Barre de 960 × 66 ; popup propriétaire non modal de 320 × 260, créé seulement à l'ouverture du menu et détruit à sa fermeture. |
| `native` | Témoin WPF de 960 × 500 avec quelques contrôles natifs et du vide transparent. Il vérifie le chemin natif sans WebView ; sa silhouette différente ne valide pas le rendu web. |
| `opaque` | Même scénario web unique, avec fond hôte opaque. Contrôle négatif : les entrées dans ce rectangle doivent être interceptées. |
| `witness` | Éditeur natif de 100 lignes, compteurs et journal des événements réellement reçus, dans un processus indépendant. |

Faire les essais web avec les deux renderers. `composition` utilise
`WebView2CompositionControl` dans une fenêtre WPF transparente ; `hwnd` utilise
le contrôle WebView2 standard, comme comparaison de mode d'hébergement. Une
réussite de l'un ne doit pas être attribuée à l'autre ni à MAUI.

Le popup `split` se ferme lorsque la barre est déplacée ou change de DPI. C'est
le comportement limité du prototype : aucun suivi continu du popup pendant un
déplacement n'est revendiqué. Tester ensuite sa réouverture et son ancrage.
Échap demande la fermeture avec retour au déclencheur ; une désactivation de
l'ensemble barre/popup ferme le menu sans demander de reprendre le focus.
Ces intentions de code restent à vérifier en usage réel.

## Géométrie et preuves

`web/index.html` est le rendu partagé. `web/atlas.json` définit les dimensions,
le creux de 24 × 24, l'espace de 12 unités entre groupes, les coins, l'ombre,
l'ancre et les points P1–P8/C1–C2. L'atlas sert à reproduire les mesures ; il
n'est jamais appliqué comme région native d'entrée.

**F8**, prototype actif, demande un relevé ponctuel `WindowFromPoint` aux points
applicables P1–P8/C1/C2 de l'atlas. L'événement `routing-sample` donne les
coordonnées écran, HWND et PID ciblés. Aucun clic, mouvement ou défilement n'est
envoyé. Ce relevé ne prouve ni la livraison des quatre gestes ni le maintien du
focus ; son absence n'est pas un résultat de routage.

Les journaux JSONL sont écrits par défaut dans
`%TEMP%\DevToolbar-feasibility`, un fichier par mode, renderer et PID. Ils
contiennent l'environnement, les écrans, DPI, réglages souris disponibles,
positions des fenêtres, activation/désactivation, version WebView2 et événements
web ou du témoin. Les erreurs d'initialisation doivent être traitées comme des
échecs de lancement, pas comme des résultats sur le passage des clics.
Les profils WebView2 de test sont également locaux au dossier temporaire.
Lire les horodatages UTC bruts : PowerShell peut convertir automatiquement les
dates en heure locale. Avec une version prenant en charge `-DateKind` :

```powershell
Get-Content -LiteralPath '<journal.jsonl>' | ConvertFrom-Json -DateKind String
```

Avant une campagne, relever la date UTC, le commit du dépôt, la configuration
Debug/Release, le hash du binaire produit, du lockfile, du HTML et de l'atlas.
Conserver les commandes, journaux, captures utiles et observations dans un
sous-dossier de campagne. Identifier les PID distincts du témoin et du prototype.
Ne pas remplacer une observation manquante par « conforme ».

## Recette manuelle

Protocole complet de référence sur cet ordinateur :
`C:\Users\clefw\Documents\Codex\2026-10-31\task\protocole-faisabilite-dev-toolbar.md`.

1. **T0 sans prototype.** Vérifier dans le témoin clic, double-clic sélectionnant
   un mot, clic droit ouvrant le menu, saisie et défilement réel. Confirmer dans
   Bloc-notes et un navigateur indépendant. Relever le périphérique, clavier,
   délai de double-clic et réglage de défilement des fenêtres inactives.
2. **Contrôles du banc.** Exécuter `opaque` pour vérifier l'interception attendue,
   puis `native` pour isoler le comportement transparent WPF sans rendu web.
   Un journal absent sans contrôle positif n'est pas une preuve de passage.
3. **Zones vides.** Pour chaque couple `single`/`split` et
   `composition`/`hwnd`, répéter menu fermé puis ouvert. À chaque point applicable
   P1–P8, effectuer **10 répétitions de chacun des quatre gestes** : clic,
   double-clic, clic droit et défilement (essayer les deux directions).
   Vérifier l'effet dans l'application du dessous et ses événements,
   ainsi que l'absence d'action web parasite. Garder les mêmes coordonnées écran
   entre les architectures. Justifier les points non applicables.
4. **Bords.** Tester les coins, le creux, l'espace entre groupes, l'espace sous
   la barre, le coin du menu et l'ombre. Échantillonner à 1, 2 et 4 pixels physiques
   de part et d'autre d'un bord après conversion DPI. Consigner l'anticrénelage
   séparément. Le critère souhaité est : alpha nul et ombre purement décorative
   traversables ; contour visible des contrôles interactif.
5. **Contrôles web.** À C1 et C2, vérifier exactement une incrémentation par clic
   et aucune entrée dans le témoin. Ouvrir/fermer rapidement le menu, cocher la
   case, saisir, sélectionner et éditer le texte, ouvrir son menu contextuel,
   puis faire défiler la liste sans faire défiler l'application derrière.
   Évaluer la réactivité à froid et après plusieurs cycles ; un horodatage
   d'événement seul n'est pas une mesure de latence visuelle.
6. **Clavier et focus.** Vérifier Tab/Maj+Tab, Entrée, Espace et flèches de la
   liste, puis Échap depuis le bouton, la case, le champ et la liste. Observer
   la fenêtre active avant et après ouverture/fermeture, et la destination de la
   prochaine frappe. Cliquer dans une autre application : elle doit recevoir
   ce premier clic et garder le focus après fermeture du menu. Essayer aussi un
   clic dans un coin transparent du popup, le déclencheur, la barre et hors des
   deux fenêtres. Vérifier Alt+Tab et les menus contextuels, sans boucle de
   fermeture/réouverture ni reprise de focus inattendue.
   Pour la vague `-NoShadow`, ouvrir d'abord le menu par un vrai clic puis faire
   ces essais à la main, en observant la destination de la frappe suivante.
   Un clic piloté par CUA avec activation préalable ne valide pas le clic extérieur.
7. **Déplacement/DPI/écrans.** Utiliser la poignée ; vérifier les boutons et
   l'absence d'action dessous. En `split`, rouvrir le menu après déplacement ou
   changement de DPI et tester son ancrage aux bords de zone de travail.
   Répéter les contrôles sur les écrans/échelles disponibles, y compris frontière
   à DPI différents et coordonnées négatives si présents. Déclarer les autres
   cas non vérifiés. Refaire les cas essentiels à froid en Release, DevTools
   fermés, session normale, puis quitter toutes les fenêtres du spike.

Pour chaque ligne de résultat, conserver : variante, renderer, écran/DPI, état
du menu, point/coordonnées physiques, entrée, résultat attendu, résultat observé,
nombre d'essais et référence au journal. Utiliser `réussi`, `échoué`, `non vérifié`
ou `non applicable justifié`. Un bilan de build n'est pas un bilan natif.

**Limite CUA de cette session :** l'outil active automatiquement la fenêtre ciblée
et refuse un clic dont le point appartient à une autre fenêtre. Ce refus ne
mesure pas le routage Windows. Premier clic extérieur et conservation du focus
nécessitent un essai manuel sans activation préalable. Un clic DOM programmatique
ne remplace pas une entrée de bureau entre deux processus.

## Limites documentaires

| Piste | Limite officielle utile à cette comparaison |
|---|---|
| Electron | Les zones transparentes ne sont pas traversables avec `transparent:true` seul. `setIgnoreMouseEvents` vise toute la fenêtre ; l'exemple sélectif officiel bascule sur les entrées/sorties de souris. `setShape` découpe dessin et entrées et reste expérimental. [Styles](https://www.electronjs.org/docs/latest/tutorial/custom-window-styles#limitations), [API](https://www.electronjs.org/docs/latest/api/browser-window), [interactions](https://www.electronjs.org/docs/latest/tutorial/custom-window-interactions). |
| Tauri | `transparent` décrit le rendu ; `set_ignore_cursor_events(bool)` ignore le curseur pour la fenêtre entière. Ces API ne promettent pas de passage sélectif par alpha. [Configuration](https://v2.tauri.app/reference/config/#transparent), [API Rust](https://docs.rs/tauri/2.12.5/tauri/window/struct.Window.html#method.set_ignore_cursor_events). |
| Win32 | Le hit-test d'une véritable layered window laisse passer les zones color-key ou d'alpha zéro. Cela ne se déduit pas d'un fond WebView transparent. Une ombre non nulle peut donc intercepter malgré son rôle décoratif. `HTTRANSPARENT` décrit une propagation aux fenêtres du même thread, pas une garantie vers un processus indépendant. [Layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows), [WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest). |
| WebView2 | `DefaultBackgroundColor` accepte alpha 0 ou 255 pour cette propriété et concerne le rendu sur le contenu hôte. Cela ne définit pas le routage natif et ne restreint pas l'alpha CSS de la page. [API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2controller.defaultbackgroundcolor). |
| WPF composition | `WebView2CompositionControl` rend dans une `Image` via une `GraphicsCaptureSession` interne au contrôle. Microsoft signale un framerate potentiellement inférieur au contrôle standard. Son association avec `AllowsTransparency` est une hypothèse à mesurer pour les entrées, pas une garantie officielle. [Contrôle](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.wpf.webview2compositioncontrol?view=webview2-dotnet-1.0.3179.45), [fenêtre WPF](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0). |

Le spike n'ajoute aucun hook global, surveillance/polling permanent de la souris,
masque natif calculé à la main ou réinjection des clics. La capture interne du
contrôle WPF ne reconstruit pas chez nous une région d'entrée. Si la conformité
nécessite un de ces contournements exclus, le consigner et consulter Benoit avant
de poursuivre cette piste.

La séparation barre/popup peut supprimer le grand rectangle vide quand le menu
est fermé ; elle ne prouve pas que les coins, creux et ombres soient traversables.
Un menu natif changerait le rendu attendu et demande une décision de Benoit.
Blazor Hybrid est officiellement disponible pour MAUI, WPF et WinForms, mais la
réutilisation des composants du dépôt nécessite une campagne ultérieure distincte.
[Blazor Hybrid](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/?view=aspnetcore-10.0).

Le verdict reste limité à Windows, aux versions et conditions effectivement
essayées. Aucun résultat macOS, Linux/X11 ou Wayland ne doit en être déduit.
