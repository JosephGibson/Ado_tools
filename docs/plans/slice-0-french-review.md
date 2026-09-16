# Slice 0 French review

Status: **pending fluent-speaker review**. Date: 2026-09-15.

The same keys and values occur in the Core and PowerShell `Resources/Strings.fr.resx`
catalogs. Tests verify key and placeholder parity; they do not establish translation quality.

| Key | French text |
| --- | --- |
| `OperationFailed` | L’opération a échoué. |
| `InvalidCollectionUrl` | Indiquez une URL de collection HTTP ou HTTPS absolue, sans informations utilisateur, paramètres de requête, fragment ni segments d’API ou d’interface. |
| `ServicesUnsupported` | Azure DevOps Services n’est pas pris en charge. Indiquez une URL de collection Azure DevOps Server. |
| `PlainHttp` | Cette collection utilise HTTP. Les échanges d’authentification Windows ne sont pas protégés par TLS. |
| `InvalidConfiguration` | La configuration n’est pas valide : {0} |
| `UnknownConfiguration` | Propriété de configuration inconnue conservée : {0} |
| `NewerConfiguration` | Le schéma de configuration {0} est plus récent que le schéma 1 pris en charge. Les modifications sont refusées. |
| `FileOutput` | Impossible d’écrire le fichier : {0} |
| `UnsupportedCulture` | La culture {0} n’est pas prise en charge ; l’anglais sera utilisé. |
| `MissingProfile` | Le profil {0} est introuvable. |
| `NoConnection` | Connectez-vous avec Connect-Ado ou indiquez une connexion. |
| `NoTarget` | Indiquez une URL de collection ou un profil configuré. |
| `ConnectionMismatch` | L’objet d’entrée appartient à une autre collection. |
| `Authentication` | L’authentification a échoué. Vérifiez l’identité Windows, l’URL de collection et la configuration du proxy ou de Negotiate. |
| `Authorization` | L’accès a été refusé. Vérifiez les autorisations de la collection et du projet. |
| `NotFound` | La ressource demandée est introuvable. |
| `Request` | Le serveur a rejeté la requête. |
| `RegistryMismatch` | Vérifiez le registre des points de terminaison et la version d’API demandée. |
| `Redirect` | Une redirection a été bloquée. Destination : {0} |
| `Throttled` | Le serveur limite les requêtes. Réessayez plus tard. |
| `Server` | Le serveur n’a pas pu traiter la requête. |
| `Timeout` | L’opération a dépassé le délai imparti. |
| `ResponseFormat` | Le format de la réponse du serveur n’est pas valide. |
| `PagingGuard` | La pagination n’a pas progressé ou a dépassé la limite de pages. |
| `Retry` | Nouvelle tentative pour {0} ; tentative {1}, motif {2}. |
| `RequestLog` | {0} {1} {2} {3} ms tentative={4} |
| `PageLog` | {0} : page {1}, éléments {2}. |
| `ConnectionHint` | L’appel des projets a échoué. Vérifiez que l’URL indiquée correspond à une collection plutôt qu’à un projet. |
| `SetProfile` | Enregistrer le profil |
| `RemoveProfile` | Supprimer le profil |
| `Connect` | Connexion à la collection établie. |
| `Disconnect` | Déconnexion de la collection effectuée. |

## Command help

Review the synopsis, description, parameter descriptions, and examples in all eight
[French command help sources](../commands/fr-CA/Connect-Ado.md):

- [Connect-Ado](../commands/fr-CA/Connect-Ado.md)
- [Disconnect-Ado](../commands/fr-CA/Disconnect-Ado.md)
- [Get-AdoConnection](../commands/fr-CA/Get-AdoConnection.md)
- [Test-AdoConnection](../commands/fr-CA/Test-AdoConnection.md)
- [Get-AdoProfile](../commands/fr-CA/Get-AdoProfile.md)
- [Set-AdoProfile](../commands/fr-CA/Set-AdoProfile.md)
- [Remove-AdoProfile](../commands/fr-CA/Remove-AdoProfile.md)
- [Get-AdoProject](../commands/fr-CA/Get-AdoProject.md)

One French MAML help file is generated into `fr/`; V-17 confirms lookup from
`fr-CA`, `fr-FR`, and `fr`. Resource catalogs use the neutral `fr` satellite.

