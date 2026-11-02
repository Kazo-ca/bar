# Intégration au projet principal — choix d'hôte Windows requis

Le 1 novembre 2026, Benoit a déclaré « tout fonctionne comme je le veux » pour le scénario manuel sans ombre : premier clic extérieur reçu par le témoin, fermeture du menu et molette fonctionnelle. Il a demandé d'appliquer cette approche au projet principal. Ce retour ne qualifie pas les autres scénarios du protocole.

## Décision concrète à prendre

**Proposition : ajouter `src/DevToolbar.Windows`, hôte WPF Windows parallèle, réutilisant `DevToolbar.Core`, `DevToolbar.UI` et `DevToolbar.Plugins`.** Conserver l'hôte MAUI et l'hôte web existants, puis décider ultérieurement du remplacement éventuel de MAUI sous Windows.

L'autre contrainte possible est de conserver impérativement l'hôte MAUI/WinUI actuel. Dans ce cas, le mécanisme WPF validé ne peut pas être appliqué directement ; une nouvelle faisabilité de l'hébergement WinUI serait nécessaire. Une simple suppression d'ombre CSS ne transforme pas ce dernier en fenêtre WPF layered.

Cette décision reste **en attente**. Aucun nouvel hôte principal, remplacement de MAUI ou changement des services métier n'a été implémenté avant ce choix.

## Pourquoi ce choix est nécessaire

Le code actuel crée un `BlazorWebView` MAUI/WinUI et lui affecte un fond alpha 255 dans `MainPage.cs`. `WindowHelper` impose une région Win32 manuelle à la fenêtre haute de 500 unités ; cette région est étendue pour certains menus. `App.xaml.cs` ouvre aussi une seconde barre XAML. Ce chemin diffère du prototype WPF sans masque testé par Benoit.

La voie WPF peut conserver **Blazor Hybrid** : le [contrôle WPF Microsoft 10.0.110](https://raw.githubusercontent.com/dotnet/maui/10.0.110/src/BlazorWebView/src/Wpf/BlazorWebView.cs) utilise déjà `WebView2CompositionControl`, y compris son template interne. Le [manager officiel](https://raw.githubusercontent.com/dotnet/maui/10.0.110/src/BlazorWebView/src/SharedSource/WebView2WebViewManager.cs) utilise cette composition dans la branche WPF. Aucun fork, adaptateur Blazor personnalisé ou serveur HTTP local ne serait requis.

Le [paquet officiel WPF 10.0.110](https://www.nuget.org/packages/Microsoft.AspNetCore.Components.WebView.Wpf/10.0.110) dépend du paquet de base WebView et de WebView2 au moins 1.0.3179.45. Les paquets WPF/base ne sont pas présents dans le cache local examiné. Leur restauration sera nécessaire si cette voie est choisie ; aucun paquet WPF n'a été installé pendant cet audit.

## Changements proposés après décision

| Partie | Travail délimité |
|---|---|
| Nouvel hôte Windows | Fenêtre WPF `AllowsTransparency`, fond alpha zéro du contrôle Blazor, une seule barre au démarrage ; réutiliser les composants Razor et les assets RCL. |
| Apparence | Feuille propre à cet hôte supprimant les ombres extérieures de l'UI réelle ; conserver les formes CSS comme source unique de rendu et d'alpha. |
| Menus | Gestion partagée de fermeture sur désactivation native, clic extérieur et Échap ; retour au déclencheur seulement pour Échap. Aucun overlay invisible consommant le premier clic. |
| Navigation | Racine de routage pour la barre et `/settings`, avec assets et scripts Blazor Hybrid. |
| Services | Réutiliser processus, fichiers, scripts, configuration, événements et plugins ; adapter les fonctions liées à MAUI et au HWND. Conserver explicitement les mocks métier déjà présents sans les présenter comme des services réels. |
| Région / docking | Aucun `SetWindowRgn` dans la voie WPF ; distinguer hauteur réservée par AppBar et hauteur de la fenêtre web afin de préserver les menus. Découpler l'obtention du HWND de MAUI. |
| Vérifications | Build des hôtes concernés, tests de l'UI web partagée, lancement Windows avec témoin externe, menus/paramètres/clavier/focus/clics/molette et transitions DPI. |

Les panneaux de rapport et de terminal ont des overlays couvrant la fenêtre ; leur rendu et leur ciblage devront être examinés pendant l'intégration. Les modifier silencieusement en une nouvelle interface n'est pas inclus dans cette proposition. Aucun gestionnaire de prompts ni nouvel outil métier n'est prévu.

## État de la vérification du principal

Le dépôt était propre avant le spike et aucun fichier suivi n'a été modifié pendant l'audit d'intégration. Le restore Windows MAUI a été tenté hors ligne, avec la même isolation de configuration NuGet que le spike :

```powershell
dotnet restore src/DevToolbar.Maui/DevToolbar.Maui.csproj `
  -p:TargetFrameworks=net10.0-windows10.0.19041.0 `
  --source "$env:USERPROFILE/.nuget/packages" --ignore-failed-sources `
  -p:NuGetAudit=false
```

**Échec de restauration, compilation MAUI non exécutée.** Le cache manque notamment de `System.Drawing.Common`, `Microsoft.AspNetCore.Components.WebView`, Win2D 1.4.0, Windows SDK BuildTools 10.0.26100.8249 et WindowsAppSDK 1.8.260529003. Le paquet Components.Web 10.0.2 demandé est aussi absent et le restore a signalé un repli 10.0.5. Il s'agit d'un cache incomplet ; aucune conclusion sur des erreurs de compilation du code principal n'est établie.

Les seuls changements exécutables réalisés à ce stade restent ceux du banc sans ombre. Le [bilan de cette variante](RESULTS-NO-SHADOW.md) et les preuves de la campagne précédente sont conservés séparément. Aucun commit, fusion ou déploiement n'a été effectué.
