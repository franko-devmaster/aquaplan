# Audit backend .NET — Fable 5

> Audit exhaustif du backend AquaPlan (.NET 10 / ASP.NET Core / EF Core / PostgreSQL).
> Périmètre : controllers API, services Application/Infrastructure, entités Domain, configurations & migrations EF, tests xUnit.
> Date : 2026-06-11 — Branche `Main`. Tous les chemins sont relatifs à la racine du repo.

## Synthèse

- **Critical : 6 findings**
- **High : 15 findings**
- **Medium : 24 findings**
- **Low : 17 findings**
- **Total : 62 findings**

Les problèmes les plus graves se concentrent sur : (1) des secrets par défaut codés en dur exploitables en production, (2) plusieurs ruptures d'isolation tenant (création d'utilisateurs, backfill LIMS, attribution de rôles), (3) des endpoints d'écriture sensibles sans aucune autorisation au-delà de `[Authorize]`, et (4) une divergence majeure entre les deux chemins de transmission LIMS (bulk vs tournée) qui corrompt silencieusement le cycle de vie des mandats.

---

## Critical

### F-001 — Secret JWT de fallback codé en dur (forge de tokens possible)
- Fichier : `src/AquaPlan.Api/Program.cs:56` (+ `src/AquaPlan.Api/appsettings.json:18`, `docker-compose.synology.yml:28`)
- Sévérité : Critical — Effort : S
- Raison : si `Jwt:SecretKey` n'est pas surchargé, la validation JWT accepte des tokens signés avec un secret **public** (commité dans le repo). `TokenService` lève une exception à la génération si le secret manque, mais la **validation**, elle, retombe silencieusement sur le secret de dev : un attaquant connaissant le repo peut forger un token Administrator avec un `tenant_id` arbitraire. Le compose Synology (cible prod) a aussi un défaut `JWT_SECRET:-AquaPlan-Prod-Secret-Key-Min-32-Chars!!` commité.
- Snippet :
  ```csharp
  // Program.cs:55-56
  var jwtSettings = builder.Configuration.GetSection("Jwt");
  var secretKey = jwtSettings["SecretKey"] ?? "AquaPlan-Dev-Secret-Key-Min-32-Chars!!";
  ...
  IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
  ```
  ```yaml
  # docker-compose.synology.yml:28
  Jwt__SecretKey: "${JWT_SECRET:-AquaPlan-Prod-Secret-Key-Min-32-Chars!!}"
  ```
- Proposition : `?? throw new InvalidOperationException("Jwt:SecretKey missing")` dans Program.cs ; supprimer les valeurs par défaut des compose ; faire tourner le secret actuel.

### F-002 — Compte admin seedé avec mot de passe codé en dur à chaque démarrage
- Fichier : `src/AquaPlan.Infrastructure/Data/Seeds/RoleAndPermissionSeeder.cs:76-95` (appelé inconditionnellement par `src/AquaPlan.Api/Program.cs:156`)
- Sévérité : Critical — Effort : S
- Raison : `admin@aquaplan.ch` / `Admin123!` est créé au boot **dans tous les environnements**, y compris prod. Credentials publics (commités), rôle Administrator complet. Combiné à F-111 (pas de lockout), c'est un accès admin garanti.
- Snippet :
  ```csharp
  var adminEmail = "admin@aquaplan.ch";
  if (await userManager.FindByEmailAsync(adminEmail) is null)
  {
      var admin = new AppUser { UserName = adminEmail, Email = adminEmail, ... };
      var result = await userManager.CreateAsync(admin, "Admin123!");
      if (result.Succeeded)
          await userManager.AddToRoleAsync(admin, RoleName.Administrator);
  }
  ```
- Proposition : conditionner le seed à `IHostEnvironment.IsDevelopment()` ou lire le mot de passe depuis la config/secret store ; forcer un changement au premier login.

### F-003 — Backfill LIMS cross-tenant : TenantId pris du body client
- Fichier : `src/AquaPlan.Api/Controllers/Api/AdminLimsSyncController.cs:48-61`
- Sévérité : Critical — Effort : XS
- Raison : violation directe de la règle d'isolation tenant de CLAUDE.md. Un Administrator du tenant A peut déclencher un backfill (écriture : `LimsOrderId`, `TransmittedAt`, transition `Done`, création de résultats) sur les mandats du tenant B en passant `TenantId` dans le body.
- Snippet :
  ```csharp
  public async Task<ActionResult<MockLimsBackfillResultDto>> BackfillResults(
      [FromBody] MockLimsBackfillRequestDto? request, CancellationToken cancellationToken)
  {
      var callerTenantId = GetTenantId();
      var tenantId = request?.TenantId ?? callerTenantId;   // ← cross-tenant
      ...
      var result = await backfillService.BackfillAsync(tenantId, maxOrders, cancellationToken);
  ```
- Proposition : ignorer `request.TenantId` et utiliser exclusivement `callerTenantId` ; retirer la propriété du DTO.

### F-004 — Création d'utilisateur cross-tenant avec rôle arbitraire
- Fichier : `src/AquaPlan.Api/Controllers/Api/UsersController.cs:59-66` + `src/AquaPlan.Infrastructure/Services/UserManagementService.cs:94-131`
- Sévérité : Critical — Effort : S
- Raison : `UserCreateDto.TenantId` vient du client et n'est jamais comparé au tenant de l'appelant. Un Administrator du tenant A peut créer un utilisateur (y compris `Role = "Administrator"`) **dans n'importe quel tenant**, avec un `DistributorId` non validé. `dto.Role` n'est pas non plus validé contre `RoleName.All`.
- Snippet :
  ```csharp
  // UsersController.CreateUser — tenantId de l'appelant jamais utilisé
  var createdBy = GetUserId();
  var user = await userManagementService.CreateUserAsync(dto, createdBy, cancellationToken);
  // UserManagementService.CreateUserAsync
  var user = new AppUser { ..., TenantId = dto.TenantId, ... };   // ← client-controlled
  ```
- Proposition : passer `GetTenantId()` au service et forcer `user.TenantId = callerTenantId` ; valider `dto.Role` contre `RoleName.All` et `dto.DistributorId` contre le tenant.

### F-005 — Bulk validate/transmit/finalize : aucun scoping distributeur → corruption de masse
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:734-740` (endpoints `OrdersController.cs:270-306`)
- Sévérité : Critical — Effort : M
- Raison : `BulkTransitionAsync` sélectionne **tous les mandats du tenant** dans le statut source. Les endpoints sont ouverts aux rôles `Requérant`/`Requérant-Préleveur` (pas seulement admin) : un requérant d'un petit distributeur peut valider puis **transmettre au LIMS** tous les mandats InProgress/Completed de tous les autres distributeurs du canton. Corruption de workflow irréversible (Transmitted → seul Done est permis).
- Snippet :
  ```csharp
  private async Task<BulkTransitionResultDto> BulkTransitionAsync(
      string userId, Guid tenantId, OrderStatus fromStatus, OrderStatus toStatus, ...)
  {
      var orders = await dbContext.Orders
          .Where(o => o.TenantId == tenantId && o.Status == fromStatus)  // ← tout le tenant
          .ToListAsync(cancellationToken);
  ```
- Proposition : pour les non-admins, restreindre aux mandats des distributeurs autorisés (`GetAuthorizedDistributorIdsForUserAsync`), comme le fait déjà `GetOrdersFilteredAsync`.

### F-006 — OIDC : access token + refresh token passés en query string de redirection
- Fichier : `src/AquaPlan.Api/Controllers/Api/AuthController.cs:156`
- Sévérité : Critical — Effort : M
- Raison : les tokens transitent dans l'URL → persistés dans l'historique navigateur, les logs de proxy/nginx (la stack a un reverse proxy nginx), les en-têtes Referer. Le refresh token (7 jours de validité) est le plus exposé.
- Snippet :
  ```csharp
  user.RefreshToken = refreshToken;
  user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenExpirationDays);
  await userManager.UpdateAsync(user);
  return Redirect($"/#/auth/callback?token={accessToken}&refresh={refreshToken}");
  ```
- Proposition : poser un cookie `HttpOnly`/`Secure` court-vécu ou un code à usage unique échangé via POST `/api/auth/exchange` ; ne jamais mettre de token dans une URL.

---

## High

### F-101 — OIDC : liaison de compte par e-mail non vérifié (account takeover)
- Fichier : `src/AquaPlan.Infrastructure/Services/OidcUserService.cs:36-44`
- Sévérité : High — Effort : M
- Raison : si l'IdP fournit un e-mail (non nécessairement vérifié) correspondant à un compte local existant, l'`ExternalId` est lié sans aucune preuve de possession → prise de contrôle du compte local (y compris admin) via un compte OIDC forgé.
- Snippet :
  ```csharp
  var emailUser = await userManager.FindByEmailAsync(email);
  if (emailUser is not null)
  {
      emailUser.ExternalId = externalId;     // ← liaison aveugle
      await userManager.UpdateAsync(emailUser);
      return emailUser;
  }
  ```
- Proposition : n'autoriser la liaison que si la claim `email_verified` est vraie ET l'IdP est de confiance, ou exiger une confirmation explicite de l'utilisateur connecté.

### F-102 — OIDC : `IsActive` jamais vérifié au login
- Fichier : `src/AquaPlan.Api/Controllers/Api/AuthController.cs:143-156`
- Sévérité : High — Effort : XS
- Raison : un utilisateur désactivé (`DeactivateUser`) peut continuer à se connecter via le flux OIDC : `FindOrCreateFromExternalLoginAsync` ne vérifie pas `IsActive`, et le callback émet directement les tokens. Le login par mot de passe vérifie pourtant ce flag (`AuthService.LoginAsync`).
- Snippet :
  ```csharp
  var user = await oidcUserService.FindOrCreateFromExternalLoginAsync(
      externalId, email, firstName, lastName, defaultTenantId, cancellationToken);
  var roles = await authService.GetUserRolesAsync(user.Id, cancellationToken);
  var accessToken = tokenService.GenerateAccessToken(user, roles);  // ← pas de check IsActive
  ```
- Proposition : `if (!user.IsActive) return Redirect("/#/login?error=account_disabled");` après la résolution de l'utilisateur.

### F-103 — Attribution/retrait de rôle sans scoping tenant
- Fichier : `src/AquaPlan.Api/Controllers/Api/RolesController.cs:40-60` + `src/AquaPlan.Infrastructure/Services/PermissionService.cs:51-91`
- Sévérité : High — Effort : S
- Raison : `AssignRoleToUserAsync(userId, roleName)` charge l'utilisateur par ID sans vérifier son tenant. Un Administrator du tenant A peut donner `Administrator` à n'importe quel utilisateur du tenant B (ou lui retirer ses rôles).
- Snippet :
  ```csharp
  public async Task<bool> AssignRoleToUserAsync(string userId, string roleName, ...)
  {
      var user = await userManager.FindByIdAsync(userId);   // ← pas de filtre tenant
      if (user is null) return false;
      ...
      var result = await userManager.AddToRoleAsync(user, roleName);
  ```
- Proposition : passer le `tenantId` de l'appelant et vérifier `user.TenantId == tenantId` avant toute modification ; valider `roleName` contre `RoleName.All`.

### F-104 — `POST /api/orders/{id}/assign` : aucune restriction de rôle ni validation du préleveur
- Fichier : `src/AquaPlan.Api/Controllers/Api/OrdersController.cs:211-222` + `src/AquaPlan.Infrastructure/Services/OrderService.cs:381-426`
- Sévérité : High — Effort : S
- Raison : contrairement à `CreateOrder`/`BulkValidate` (rôles restreints), `AssignPreleveur` est accessible à tout utilisateur authentifié du tenant, sans vérification d'accès au mandat. `dto.PreleveurId` n'est pas validé (existence, tenant, rôle Préleveur) : on peut assigner un ID arbitraire, voire un utilisateur d'un autre tenant.
- Snippet :
  ```csharp
  [HttpPost("{id:guid}/assign")]
  public async Task<ActionResult<OrderDetailDto>> AssignPreleveur(Guid id, [FromBody] OrderAssignDto dto, ...)
  {
      var tenantId = GetTenantId();
      var updatedBy = GetUserId();
      var order = await orderService.AssignPreleveurAsync(id, dto, updatedBy, tenantId, cancellationToken);
  ```
- Proposition : ajouter `[Authorize(Roles = ...)]` + check `UserCanAccessOrderAsync` ; dans le service, vérifier que `PreleveurId` est un utilisateur actif du tenant avec un rôle préleveur.

### F-105 — `POST /api/orders/{id}/transition` : pas de contrôle d'accès + chemin Transmitted incohérent
- Fichier : `src/AquaPlan.Api/Controllers/Api/OrdersController.cs:224-243` + `src/AquaPlan.Infrastructure/Services/OrderStatusService.cs:62-125`
- Sévérité : High — Effort : M
- Raison : (1) tout utilisateur authentifié du tenant peut transitionner n'importe quel mandat (annuler, compléter…) sans être ni créateur, ni préleveur, ni admin. (2) La transition `Completed → Transmitted` par ce endpoint ne pousse PAS le mandat vers le Mock LIMS et ne renseigne pas `TransmittedAt` (contrairement à `BulkTransitionAsync`), produisant des mandats `Transmitted` sans `LimsOrderId` que le worker ne pullera jamais.
- Snippet :
  ```csharp
  order.Status = newStatus;
  order.StatusChangedAt = transitionDate;
  order.StatusChangedBy = userId;
  // ← aucun TransmittedAt, aucun TransmitOrdersToMockLimsAsync si newStatus == Transmitted
  await dbContext.SaveChangesAsync(cancellationToken);
  ```
- Proposition : ajouter le même contrôle d'accès que `UpdateOrder` ; centraliser l'effet de bord LIMS (`TransmittedAt` + forward) dans un seul chemin partagé par transition unitaire, bulk et tournée.

### F-106 — Endpoints d'écriture des tournées ouverts à tous les utilisateurs authentifiés
- Fichier : `src/AquaPlan.Api/Controllers/Api/SamplingRoundsController.cs:81-110, 124-133, 183-229`
- Sévérité : High — Effort : M
- Raison : `UpdateRound`, `DeleteRound`, `AssignPreleveur`, `TransmitAll`, `CancelRound`, `ReorderOrders` n'ont aucune restriction de rôle ni vérification d'appartenance au distributeur. Un préleveur "seul" (ou n'importe quel compte du tenant) peut annuler une tournée entière — ce qui **annule tous ses mandats** (`CancelAsync` force `OrderStatus.Cancelled`) — ou assigner un préleveur arbitraire.
- Snippet :
  ```csharp
  [HttpPost("{id:guid}/cancel")]              // ← pas de [Authorize(Roles=...)]
  public async Task<ActionResult<SamplingRoundDetailDto>> CancelRound(Guid id, ...)
  {
      var result = await samplingRoundService.CancelAsync(id, userId, tenantId, cancellationToken);
  ```
- Proposition : aligner sur `CreateRound` : `[Authorize(Roles = Admin,Requerant,RequerantPreleveur)]` + vérification `GetAuthorizedDistributorIdsForUserAsync` pour les non-admins.

### F-107 — `DELETE /api/sampling-rounds/{id}` supprime physiquement tous les mandats de la tournée
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingRoundService.cs:200-222`
- Sévérité : High — Effort : S
- Raison : la suppression d'une tournée Draft fait un hard-delete de chaque `Order` lié (et en cascade ses `OrderAnalysisPrograms`). Combiné à F-106 (endpoint sans rôle), n'importe quel compte du tenant peut détruire des mandats. Comportement incohérent avec `RemoveOrderAsync` (AQ-413) qui détache sans supprimer.
- Snippet :
  ```csharp
  foreach (var order in round.Orders.ToList())
  {
      dbContext.Orders.Remove(order);     // ← hard delete des mandats
  }
  dbContext.SamplingRounds.Remove(round);
  ```
- Proposition : détacher les mandats (`SamplingRoundId = null`) au lieu de les supprimer, ou exiger qu'une tournée soit vide pour être supprimée.

### F-108 — `TransmitAllAsync` (tournée) ne pousse pas au LIMS et ne met pas à jour les champs de statut
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingRoundService.cs:627-656`
- Sévérité : High — Effort : M
- Raison : c'est la cause racine du besoin de backfill AQ-404. La transmission par tournée passe les mandats à `Transmitted` **sans** : appel Mock LIMS (pas de `LimsOrderId`), `TransmittedAt`, `StatusChangedAt/By`, ni écriture d'audit log — quatre divergences avec `OrderService.BulkTransitionAsync`. Les résultats ne seront jamais récupérés par le worker (`LimsOrderId != null` requis).
- Snippet :
  ```csharp
  foreach (var order in completedOrders)
  {
      order.Status = OrderStatus.Transmitted;
      order.UpdatedAt = DateTime.UtcNow;
      order.UpdatedBy = userId;
      // ← pas de TransmittedAt, pas de StatusChangedAt/By, pas de LIMS, pas d'audit
  }
  ```
- Proposition : déléguer la transition à un service commun (celui utilisé par `BulkTransmitAsync`) au lieu de muter les statuts à la main.

### F-109 — Génération des mandats depuis un plan non idempotente (duplication massive)
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingPlanService.cs:353-454`
- Sévérité : High — Effort : S
- Raison : après `GenerateOrdersFromPlanAsync`, le plan reste en statut `Validated`. Un second appel (double-clic, retry réseau, replay) regénère **tous** les mandats (items × mois) en doublon. Aucun verrou ni marqueur `OrdersGeneratedAt`.
- Snippet :
  ```csharp
  if (plan.Status != SamplingPlanStatus.Validated)
  {
      throw new InvalidOperationException(...);
  }
  ...
  // génère item.PlannedMonths × items mandats — et le plan reste Validated
  ```
- Proposition : introduire un statut `OrdersGenerated` (ou un champ `OrdersGeneratedAt`) vérifié/posé dans la même transaction.

### F-110 — Race condition sur la génération du numéro de mandat
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:924-941`
- Sévérité : High — Effort : M
- Raison : lecture du max puis insertion sans verrou : deux créations concurrentes (fréquent avec `GenerateOrdersFromPlanAsync` qui crée en boucle) produisent le même `OrderNumber` → l'index unique `orders.order_number` fait échouer la 2e en `DbUpdateException` → 500 non géré. `int.Parse(lastPart)` lèvera aussi `FormatException` si un numéro legacy ne respecte pas le format.
- Snippet :
  ```csharp
  var maxNumber = await dbContext.Orders
      .Where(o => o.OrderNumber.StartsWith(prefix))
      .Select(o => o.OrderNumber)
      .MaxAsync(cancellationToken) as string;
  var nextSeq = 1;
  if (maxNumber is not null)
      nextSeq = int.Parse(maxNumber[(prefix.Length + 1)..]) + 1;
  ```
- Proposition : séquence PostgreSQL (`CREATE SEQUENCE`) ou retry sur violation d'unicité ; `int.TryParse` pour la robustesse.

### F-111 — Login sans lockout : brute force illimité
- Fichier : `src/AquaPlan.Application/Services/AuthService.cs:25-30`
- Sévérité : High — Effort : S
- Raison : `CheckPasswordAsync` ne déclenche pas le compteur d'échecs Identity (`AccessFailedCount`) ; aucun `MaxFailedAccessAttempts` configuré côté `AddIdentity`. Application gouvernementale exposée → brute force du compte admin seedé (F-002) trivial.
- Snippet :
  ```csharp
  var isValidPassword = await userManager.CheckPasswordAsync(user, dto.Password);
  if (!isValidPassword)
  {
      logger.LogWarning("Login failed for email {Email}: invalid password", dto.Email);
      return null;
  }
  ```
- Proposition : utiliser `SignInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)` et configurer `options.Lockout` dans `WithInfrastructure`.

### F-112 — Création de LDP : `DistributorId` non validé contre le tenant, endpoint ouvert à tous les rôles
- Fichier : `src/AquaPlan.Api/Controllers/Api/SamplingLocationsController.cs:60-74` + `src/AquaPlan.Infrastructure/Services/SamplingLocationService.cs:120-148`
- Sévérité : High — Effort : S
- Raison : `POST /api/sampling-locations` est accessible à tout utilisateur authentifié, et ni le controller ni le service ne vérifient que `dto.DistributorId` appartient au tenant de l'appelant ni que l'utilisateur a accès à ce distributeur. Le tenant du LDP étant dérivé du distributeur (`sl.Distributor.TenantId`), on peut **écrire dans un autre tenant**.
- Snippet :
  ```csharp
  var location = new SamplingLocation
  {
      ...
      DistributorId = dto.DistributorId,   // ← jamais validé (tenant ni autorisation)
      SectorId = dto.SectorId,
      IsValidated = isValidated,
  };
  ```
- Proposition : vérifier `Distributors.AnyAsync(d => d.Id == dto.DistributorId && d.TenantId == tenantId)` + accès distributeur pour les non-admins (même pattern AQ-369).

### F-113 — Change requests : le check d'accès ignore le distributeur primaire et les délégations
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingLocationChangeRequestService.cs:220-229`
- Sévérité : High — Effort : S
- Raison : `ValidateUserDistributorAccessAsync` ne regarde que `UserDistributors`, alors que le reste de l'app (cf. commentaire "Bug fix" dans `DelegationService.cs:108-126`) inclut `AppUser.DistributorId` (lien primaire) et les délégations actives. Les utilisateurs dont le seul lien est le primaire reçoivent un 403 injustifié ; les délégataires ne peuvent pas soumettre de demande pour le distributeur délégant.
- Snippet :
  ```csharp
  var hasAccess = await dbContext.UserDistributors
      .AnyAsync(ud => ud.UserId == userId && ud.DistributorId == distributorId, cancellationToken);
  if (!hasAccess)
      throw new UnauthorizedAccessException($"User {userId} does not have access to distributor {distributorId}.");
  ```
- Proposition : remplacer par `delegationService.GetAuthorizedDistributorIdsForUserAsync(userId).Contains(distributorId)`.

### F-114 — Endpoint de logs anonyme sans limitation : flood disque et pollution de logs
- Fichier : `src/AquaPlan.Api/Controllers/Api/LogsController.cs:9-28`
- Sévérité : High — Effort : S
- Raison : `POST /api/logs` est `[AllowAnonymous]`, sans rate limiting ni taille max dédiée : n'importe qui sur Internet peut saturer les fichiers Serilog (rotation 30 jours, disque Synology) et injecter du contenu arbitraire dans les logs consultés par les admins (`dto.Level`/`dto.Context`/`dto.Message` non normalisés au-delà du template structuré).
- Snippet :
  ```csharp
  [Route("api/[controller]")]
  [ApiController]
  [AllowAnonymous]
  public class LogsController(ILogger<LogsController> logger) : ControllerBase
  {
      [HttpPost]
      public IActionResult Post([FromBody] ClientLogDto dto)
  ```
- Proposition : exiger `[Authorize]` (le front log surtout des sessions connectées), ou ajouter un rate limiter ASP.NET (`AddRateLimiter`) + `[StringLength]` sur `Message`/`Context`.

### F-115 — Validation d'un prélèvement sans aucun contrôle de rôle
- Fichier : `src/AquaPlan.Api/Controllers/Api/SamplingsController.cs:60-68` + `src/AquaPlan.Infrastructure/Services/SamplingService.cs:240-278`
- Sévérité : High — Effort : S
- Raison : contrairement à `CreateAsync`/`UpdateAsync`/`CompleteAsync` (qui vérifient le préleveur assigné), `ValidateAsync` accepte n'importe quel `validatorId` du tenant : tout utilisateur authentifié peut marquer `IsValidated = true` sur le prélèvement de n'importe quel mandat — étape de contrôle qualité métier pourtant sensible.
- Snippet :
  ```csharp
  public async Task<bool> ValidateAsync(Guid orderId, string validatorId, Guid tenantId, ...)
  {
      var order = await dbContext.Orders ... ;
      // ← aucun check de rôle (requérant/admin) ni d'accès distributeur
      sampling.IsValidated = true;
      sampling.ValidatedAt = DateTime.UtcNow;
  ```
- Proposition : restreindre l'endpoint aux rôles requérant/admin + vérifier `UserCanAccessOrderAsync`.

---

## Medium

### F-201 — `ApiExceptionFilterAttribute` jamais enregistré : code mort qui duplique le middleware
- Fichier : `src/AquaPlan.Api/Middleware/ApiExceptionFilterAttribute.cs:7-110`
- Sévérité : Medium — Effort : S
- Raison : le filtre n'est référencé nulle part (`AddControllers` sans options, aucun attribut posé) ; tout passe par `BusinessExceptionMiddleware`. ~100 lignes dupliquant exactement le même mapping d'exceptions (RoundLocked/Forbidden/Conflict/InvalidOperation/KeyNotFound) — deux sources de vérité à maintenir, et un test entier (`ApiExceptionFilterAttributeTest`) qui teste du code mort.
- Snippet :
  ```csharp
  public class ApiExceptionFilterAttribute : ExceptionFilterAttribute
  {
      public override void OnException(ExceptionContext context)
      {
          if (context.Exception is RoundLockedException rlx) { ... }   // dupliqué du middleware
  ```
- Proposition : supprimer le filtre + son test, ou l'enregistrer et supprimer le middleware (un seul des deux).

### F-202 — Mapping global `InvalidOperationException → 400` : fuite de messages internes
- Fichier : `src/AquaPlan.Api/Middleware/BusinessExceptionMiddleware.cs:48-53`
- Sévérité : Medium — Effort : M
- Raison : toute `InvalidOperationException` (y compris celles du framework : LINQ `Single()`, DI, EF "connection disposed"…) devient un 400 dont le **message brut** est renvoyé au client. Contraire à la règle CLAUDE.md "never expose internal details". Les vraies erreurs métier devraient être typées.
- Snippet :
  ```csharp
  catch (InvalidOperationException ex)
  {
      context.Response.StatusCode = StatusCodes.Status400BadRequest;
      await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
  }
  ```
- Proposition : créer une `BusinessRuleException` dédiée (comme `ConflictOperationException`) et ne mapper que celle-ci en 400 ; `InvalidOperationException` → 500 générique.

### F-203 — `UnauthorizedAccessException → 403` avec message exposant des IDs internes
- Fichier : `src/AquaPlan.Api/Middleware/BusinessExceptionMiddleware.cs:55-61` (+ message dans `SamplingLocationChangeRequestService.cs:227`)
- Sévérité : Medium — Effort : XS
- Raison : le message `"User {userId} does not have access to distributor {distributorId}"` est renvoyé tel quel au client → divulgation d'identifiants internes utiles à l'énumération.
- Snippet :
  ```csharp
  catch (UnauthorizedAccessException ex)
  {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
  ```
- Proposition : renvoyer un message générique ("Accès refusé") et garder le détail dans les logs.

### F-204 — `GET /api/orders/{id}/audit-log` sans contrôle d'accès
- Fichier : `src/AquaPlan.Api/Controllers/Api/OrdersController.cs:373-379`
- Sévérité : Medium — Effort : XS
- Raison : tous les autres endpoints `GET` de détail vérifient `UserCanAccessOrderAsync` ; l'audit-log (qui contient les noms des intervenants, les codes-barres scannés, l'historique des actions) est lisible par tout utilisateur du tenant sur n'importe quel mandat.
- Snippet :
  ```csharp
  [HttpGet("{id:guid}/audit-log")]
  public async Task<ActionResult<List<OrderAuditLogDto>>> GetAuditLog(Guid id, ...)
  {
      var tenantId = GetTenantId();
      var logs = await orderAuditService.GetByOrderIdAsync(id, tenantId, cancellationToken);
      return Ok(logs);
  ```
- Proposition : appliquer le même bloc `hasViewAll / UserCanAccessOrderAsync` que `GetOrder`.

### F-205 — Lectures intra-tenant non scopées : `GetRound`, `GetSampling`, `GetByBarcode`, `GetFiltered` LDP
- Fichier : `src/AquaPlan.Api/Controllers/Api/SamplingRoundsController.cs:49-56`, `SamplingsController.cs:17-24, 81-88`, `SamplingLocationsController.cs:30-38`
- Sévérité : Medium — Effort : M
- Raison : le listing des tournées applique AQ-398 (scoping par rôle), mais l'accès par ID renvoie n'importe quelle tournée/prélèvement du tenant à n'importe quel utilisateur (préleveur seul compris). Idem `GET /api/samplings/by-barcode/{barcode}` et `GET /api/sampling-locations/filtered`. Divulgation intra-tenant incohérente avec la politique de visibilité du reste de l'app.
- Snippet :
  ```csharp
  [HttpGet("{id:guid}")]
  public async Task<ActionResult<SamplingRoundDetailDto>> GetRound(Guid id, ...)
  {
      var tenantId = GetTenantId();
      var result = await samplingRoundService.GetByIdAsync(id, tenantId, cancellationToken);  // ← pas de scoping rôle
  ```
- Proposition : réutiliser le pattern admin/préleveur-only/mandataire de `GetRounds` sur les accès unitaires.

### F-206 — Export PDF des LDP : IDOR intra-tenant sur `distributorId`
- Fichier : `src/AquaPlan.Api/Controllers/Api/SamplingLocationsController.cs:149-163`
- Sévérité : Medium — Effort : S
- Raison : pour un non-admin, seul `distributorId == null` est refusé ; tout `distributorId` explicite est accepté sans vérifier que l'utilisateur est autorisé sur ce distributeur → export de la liste des LDP (adresses, descriptions d'accès aux installations d'eau potable — donnée sensible) d'un autre distributeur.
- Snippet :
  ```csharp
  var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
  if (!hasViewAll && distributorId is null)
  {
      return Forbid();
  }
  var pdfBytes = await samplingLocationService.ExportPdfAsync(tenantId, distributorId, cancellationToken);
  ```
- Proposition : pour les non-admins, vérifier `GetAuthorizedDistributorIdsForUserAsync(userId).Contains(distributorId.Value)`.

### F-207 — `POST /api/samplingplans/{id}/submit` sans contrôle d'accès distributeur
- Fichier : `src/AquaPlan.Api/Controllers/Api/SamplingPlansController.cs:167-186`
- Sévérité : Medium — Effort : XS
- Raison : `GetPlan`, `UpdatePlan`, `DeletePlan` vérifient tous `UserHasDistributorAccessAsync` ; `SubmitPlan` non. Tout utilisateur du tenant peut soumettre le plan Draft d'un autre distributeur (changement d'état métier).
- Snippet :
  ```csharp
  [HttpPost("{id:guid}/submit")]
  public async Task<ActionResult<SamplingPlanDetailDto>> SubmitPlan(Guid id, ...)
  {
      var userId = GetUserId();
      var tenantId = GetTenantId();
      // ← pas de hasViewAll / UserHasDistributorAccessAsync
      var plan = await samplingPlanService.SubmitPlanAsync(id, userId, tenantId, cancellationToken);
  ```
- Proposition : copier le bloc d'autorisation de `UpdatePlan`.

### F-208 — Export CSV : pas d'échappement (CSV/formula injection, séparateurs cassés)
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:473-500`
- Sévérité : Medium — Effort : S
- Raison : les champs (noms de distributeurs, LDP, préleveurs — saisis par les utilisateurs) sont concaténés avec `;` sans quoting : un nom contenant `;` décale les colonnes, et une valeur commençant par `=`, `+`, `-`, `@` est exécutée comme formule à l'ouverture dans Excel (OWASP CSV injection).
- Snippet :
  ```csharp
  var line = string.Join(";",
      o.OrderNumber, o.Status, o.IsUnplanned ? "Yes" : "No",
      o.UnplannedReason?.ToString() ?? "",
      o.Distributor?.Name ?? "",          // ← non échappé, contrôlé par l'utilisateur
      o.SamplingLocation?.Name ?? "", ...);
  ```
- Proposition : quoter chaque champ (`"..."` + doublement des quotes) et préfixer `'` devant `=+-@`.

### F-209 — `GetUsersAsync` : N+1 sur les rôles + filtrage en mémoire
- Fichier : `src/AquaPlan.Infrastructure/Services/UserManagementService.cs:37-73`
- Sévérité : Medium — Effort : M
- Raison : une requête `GetRolesAsync` **par utilisateur** (N+1), et le filtre `role` est appliqué après chargement complet. Sur un tenant avec des centaines d'utilisateurs, l'écran admin et `GET /api/users/preleveurs` (appelé par les écrans d'assignation) deviennent coûteux.
- Snippet :
  ```csharp
  foreach (var user in users)
  {
      var roles = await userManager.GetRolesAsync(user);   // ← 1 requête par user
      var userRole = roles.FirstOrDefault();
      if (role is not null && !string.Equals(userRole, role, ...)) continue;
  ```
- Proposition : joindre `UserRoles`/`Roles` dans la requête EF (group join) et filtrer côté SQL.

### F-210 — `UserHasPermissionAsync` : 4 requêtes DB par check, exécuté sur quasi chaque requête API
- Fichier : `src/AquaPlan.Infrastructure/Services/PermissionService.cs:93-119`
- Sévérité : Medium — Effort : M
- Raison : chaque action de controller appelle `UserHasPermissionAsync(userId, "ViewAllOrders")`, qui fait `FindByIdAsync` + `GetRolesAsync` + requête roleIds + requête permissions. 4 round-trips DB par requête HTTP juste pour un booléen qui pourrait être une claim du JWT.
- Snippet :
  ```csharp
  public async Task<bool> UserHasPermissionAsync(string userId, string permissionName, ...)
  {
      var permissions = await GetUserPermissionsAsync(userId, cancellationToken);  // 4 requêtes
      return permissions.Contains(permissionName);
  }
  ```
- Proposition : embarquer les permissions dans les claims du token à la génération, ou cacher par requête (`IMemoryCache`/scoped cache).

### F-211 — `SamplingRoundService.GetByIdAsync` : graphe d'Include géant sans `AsSplitQuery` (explosion cartésienne)
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingRoundService.cs:50-70` (idem `TransmitAllAsync:605-623`, `OrderService.GetRequiredContainersAsync:511-519`)
- Sévérité : Medium — Effort : S
- Raison : 5 collections imbriquées (`Orders × OrderAnalysisPrograms × ProgramProfiles × …`) en mode single-query → produit cartésien : pour une tournée de 20 mandats × 3 programmes × 5 profils, chaque ligne de mandat est dupliquée des centaines de fois dans le résultat SQL. Cette méthode est appelée en plus après chaque mutation (Create/Update/Assign/…).
- Snippet :
  ```csharp
  var round = await dbContext.SamplingRounds
      .Include(sr => sr.Orders.OrderBy(o => o.SortOrder)).ThenInclude(o => o.SamplingLocation)...
      .Include(sr => sr.Orders).ThenInclude(o => o.OrderAnalysisPrograms)
          .ThenInclude(oap => oap.AnalysisProgram!).ThenInclude(ap => ap.AnalysisProgramProfiles)
              .ThenInclude(app => app.AnalysisProfile!).ThenInclude(profile => profile.Container)
      .FirstOrDefaultAsync(cancellationToken);
  ```
- Proposition : ajouter `.AsSplitQuery()` (et `AsNoTracking` pour les lectures pures).

### F-212 — `GetMatrixAsync` charge tous les mandats avec résultats sans borne
- Fichier : `src/AquaPlan.Infrastructure/Services/ResultsService.cs:99-153`
- Sévérité : Medium — Effort : M
- Raison : sans filtre de dates, la matrice charge **tous** les mandats du tenant avec `ResultsReceivedAt != null`, plus 4 Include (résultats, LDP, secteur, programmes) — croissance non bornée avec l'historique (volume annuel cantonal). Pas de `Take`, pas de fenêtre temporelle par défaut.
- Snippet :
  ```csharp
  var orders = await query
      .Include(o => o.SamplingLocation).ThenInclude(l => l!.Sector)
      .Include(o => o.SamplingLocation).ThenInclude(l => l!.Distributor)
      .Include(o => o.SamplingResults)
      .Include(o => o.OrderAnalysisPrograms).ThenInclude(oap => oap.AnalysisProgram)
      .ToListAsync(cancellationToken);     // ← non borné
  ```
- Proposition : fenêtre par défaut (p.ex. 12 mois) si `dateFrom` absent + cap dur sur le nombre de lignes.

### F-213 — Dashboard summary : une ligne + 2 sous-requêtes par mandat du tenant
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:702-731`
- Sévérité : Medium — Effort : M
- Raison : la projection rapatrie une ligne **par mandat** (avec 2 sous-requêtes `Count` corrélées chacune) puis agrège en mémoire. Une seule requête `GroupBy` SQL suffirait. Endpoint appelé sur la page d'accueil.
- Snippet :
  ```csharp
  var stats = await query
      .Select(o => new {
          o.Status,
          TotalResults = dbContext.SamplingResults.Count(r => r.OrderId == o.Id),
          NonConformResults = dbContext.SamplingResults.Count(r => r.OrderId == o.Id && !r.IsConform),
      })
      .ToListAsync(cancellationToken);   // ← N lignes + agrégation client
  ```
- Proposition : agréger côté SQL (GroupBy conditionnel) pour ne rapatrier que 3-4 compteurs.

### F-214 — Audit en masse : un `SaveChangesAsync` par mandat dans les transitions bulk
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:790-802` + `OrderAuditService.cs:31-50`
- Sévérité : Medium — Effort : S
- Raison : `auditService.LogAsync` fait un `SaveChangesAsync` par appel ; la boucle bulk fait donc N commits successifs (latence et charge DB linéaires, non transactionnel avec la transition elle-même : si un log échoue, les statuts sont déjà commités sans trace).
- Snippet :
  ```csharp
  foreach (var order in orders)
  {
      await auditService.LogAsync(order.Id, "StatusTransitioned", ..., cancellationToken);  // 1 SaveChanges chacun
  }
  ```
- Proposition : ajouter une surcharge `LogRangeAsync` qui `AddRange` + un seul `SaveChangesAsync`, idéalement dans la même transaction que la transition.

### F-215 — L'auto-complétion d'une tournée ne libère pas le verrou préleveur
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderStatusService.cs:97-107` et `OrderService.cs:813-823`
- Sévérité : Medium — Effort : XS
- Raison : `TransmitAllAsync` libère le lock à la complétion (`IsLocked = false…`), mais les deux autres chemins qui passent une tournée à `Completed` (transition unitaire et bulk) laissent `IsLocked = true` → tournée terminée mais verrouillée : écritures admin bloquées (`EnsureRoundNotLockedForWriteAsync`) jusqu'à un force-unlock manuel.
- Snippet :
  ```csharp
  if (round.Orders.All(o => o.Status is OrderStatus.Transmitted or OrderStatus.Done or OrderStatus.Cancelled))
  {
      round.Status = SamplingRoundStatus.Completed;
      round.CompletedAt = DateTime.UtcNow;
      // ← IsLocked / LockedById / LockedAt non réinitialisés (contrairement à TransmitAllAsync:646-648)
  }
  ```
- Proposition : extraire un helper `CompleteRound(round)` unique qui pose le statut ET libère le verrou.

### F-216 — Verrou de tournée sans concurrence optimiste
- Fichier : `src/AquaPlan.Domain/Entities/SamplingRound.cs:27-31` + `SamplingRoundService.StartAsync/ForceUnlockAsync`
- Sévérité : Medium — Effort : M
- Raison : `IsLocked`/`LockedById` sont des colonnes ordinaires sans token de concurrence (pas de `xmin`/rowversion). `StartAsync` (lecture-test-écriture) et `ForceUnlockAsync` peuvent s'entrelacer : un admin force-unlock pendant que le préleveur démarre → état lock/statut incohérent, écrasements silencieux (last-write-wins).
- Snippet :
  ```csharp
  if (round.Status != SamplingRoundStatus.Assigned)
      throw new ConflictOperationException(...);
  round.Status = SamplingRoundStatus.InProgress;
  round.IsLocked = true;                       // ← pas de protection concurrente
  await dbContext.SaveChangesAsync(cancellationToken);
  ```
- Proposition : `builder.Property(sr => sr.xmin).IsRowVersion()` (Npgsql `UseXminAsConcurrencyToken`) + gestion de `DbUpdateConcurrencyException` → 409.

### F-217 — Gestion des rôles utilisateur : résultats Identity ignorés + race sur `UserNumber`
- Fichier : `src/AquaPlan.Infrastructure/Services/UserManagementService.cs:107-112, 121-124, 156-164`
- Sévérité : Medium — Effort : S
- Raison : (1) `AddToRoleAsync`/`RemoveFromRolesAsync` retournent des `IdentityResult` jamais vérifiés : un `dto.Role` invalide laisse l'utilisateur **sans aucun rôle** silencieusement. (2) `UserNumber = max + 1` est une race classique (deux créations concurrentes → doublon, aucune contrainte d'unicité).
- Snippet :
  ```csharp
  var currentRoles = await userManager.GetRolesAsync(user);
  if (currentRoles.Count > 0)
      await userManager.RemoveFromRolesAsync(user, currentRoles);   // ← résultat ignoré
  if (!string.IsNullOrEmpty(dto.Role))
      await userManager.AddToRoleAsync(user, dto.Role);             // ← résultat ignoré
  ```
- Proposition : vérifier `result.Succeeded` et lever une exception métier sinon ; index unique `(TenantId, UserNumber)` + retry.

### F-218 — Approbation d'une change request : unicité du code LDP non vérifiée, `IsValidated` incohérent
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingLocationChangeRequestService.cs:144-168`
- Sévérité : Medium — Effort : S
- Raison : le chemin d'approbation crée/modifie un LDP **sans** passer par `IsLocationCodeUniqueAsync` (doublons de `LocationCode` possibles, contournant la règle du chemin direct), et le LDP créé a `IsActive = true` mais `IsValidated` laissé à `false` (défaut entité) — alors que la création directe couple les deux (`IsActive = isValidated`). Le LDP approuvé n'apparaît donc pas dans les snapshots offline (filtre `IsValidated`).
- Snippet :
  ```csharp
  case ChangeRequestType.Create:
      var newLocation = new SamplingLocation
      {
          Name = request.ProposedName!,
          LocationCode = request.ProposedLocationCode!,   // ← pas de check d'unicité
          DistributorId = request.DistributorId,
          IsActive = true,                                 // ← IsValidated reste false
      };
  ```
- Proposition : appeler `IsLocationCodeUniqueAsync` avant l'apply et poser `IsValidated = true` (la revue admin EST la validation).

### F-219 — Création de délégation sans aucune validation métier
- Fichier : `src/AquaPlan.Infrastructure/Services/DelegationService.cs:35-49`
- Sévérité : Medium — Effort : S
- Raison : aucune vérification que (1) les deux distributeurs existent et appartiennent au tenant de l'appelant (FK seulement), (2) `DelegatingDistributorId != DelegatedToDistributorId`, (3) `ValidFrom <= ValidTo`, (4) pas de doublon actif. Une délégation référençant un distributeur d'un autre tenant élargit les droits (`GetAuthorizedDistributorIdsForUserAsync` ne re-filtre pas par tenant côté requêtes Orders).
- Snippet :
  ```csharp
  var delegation = new DistributorDelegation
  {
      DelegatingDistributorId = dto.DelegatingDistributorId,    // ← non validés
      DelegatedToDistributorId = dto.DelegatedToDistributorId,
      ValidFrom = ..., ValidTo = ..., IsActive = true, TenantId = tenantId,
  };
  ```
- Proposition : valider tenant + self-delegation + cohérence des dates avant insertion.

### F-220 — DTOs d'écriture sensibles sans annotations de validation (68 fichiers)
- Fichier : `src/AquaPlan.Application/DTOs/Orders/OrderCreateDto.cs`, `OrderUpdateDto.cs`, `OrderAssignDto.cs`, `Users/UserCreateDto.cs`, etc.
- Sévérité : Medium — Effort : M
- Raison : la règle CLAUDE.md "Validate all user input at controller level using typed DTOs with data annotations" n'est appliquée que sur ~25% des DTOs (SamplingRounds, Auth, Logging…). `OrderCreateDto.Notes` sans `[StringLength(2000)]` (contrainte EF) → `DbUpdateException` 500 au lieu de 400 ; `UserCreateDto` sans `[Required]`/`[EmailAddress]` ; `OrderAssignDto.PreleveurId` sans `[Required]`.
- Snippet :
  ```csharp
  public record OrderCreateDto(
      Guid DistributorId,
      Guid? SamplingLocationId,
      string? PreleveurId,
      DateTime? PlannedDate,
      List<Guid>? AnalysisProgramIds,
      string? Notes,            // ← pas de [StringLength(2000)] (colonne EF limitée à 2000)
      bool IsUnplanned, ...);
  ```
- Proposition : passe systématique d'annotations alignées sur les `HasMaxLength` des configurations EF.

### F-221 — Pagination non bornée et `page=0` → 500
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:135-137` (idem SamplingRounds, SamplingPlans, SamplingLocations)
- Sévérité : Medium — Effort : S
- Raison : `pageSize` n'est jamais plafonné (`?pageSize=100000` charge tout le tenant avec les sous-requêtes de comptage) et `page=0` produit `Skip(-20)` → `OFFSET -20` PostgreSQL → exception 500. `NotificationService`/`LimsSyncService` clampent pourtant leur `take` — pattern non généralisé.
- Snippet :
  ```csharp
  var rows = await query
      .Skip((filter.Page - 1) * filter.PageSize)   // ← page=0 → OFFSET négatif
      .Take(filter.PageSize)                        // ← non plafonné
  ```
- Proposition : `page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);` dans les services paginés (helper commun).

### F-222 — Code mort : `GetDelegatedDistributorIdsAsync`, surcharge `GetUsersAsync(tenantId)`, projet `AquaPlan.Worker`
- Fichier : `src/AquaPlan.Infrastructure/Services/DelegationService.cs:88-106`, `UserManagementService.cs:16-35`, `src/AquaPlan.Worker/Worker.cs`
- Sévérité : Medium — Effort : S
- Raison : `GetDelegatedDistributorIdsAsync` n'est appelé que par ses tests ; la surcharge `GetUsersAsync(Guid, CancellationToken)` n'a aucun appelant ; `AquaPlan.Worker` est le template `BackgroundService` par défaut jamais implémenté (la sync LIMS tourne en réalité dans l'API via `MockLimsResultsSyncWorker`), alors que CLAUDE.md le décrit comme "Background job runner (LIMS sync, notifications)".
- Snippet :
  ```csharp
  // DelegationService.cs:88 — aucun appelant hors tests
  public async Task<List<Guid>> GetDelegatedDistributorIdsAsync(string userId, ...)
  ```
- Proposition : supprimer les deux méthodes + leurs entrées d'interface et tests ; supprimer le projet Worker ou y déplacer réellement le worker LIMS (et corriger CLAUDE.md).

### F-223 — Tests manquants sur des briques sensibles (violation de la règle "tests obligatoires")
- Fichier : `tests/AquaPlan.Api.Tests/Controllers/` (pas de `SamplingsControllerTest`, `LogsControllerTest`) ; `tests/AquaPlan.Infrastructure.Tests/Services/` (pas de `UserManagementServiceTest`, `PermissionServiceTest`, `OrderAuditServiceTest`) ; pas de `TokenServiceTest`
- Sévérité : Medium — Effort : L
- Raison : CLAUDE.md rend les tests obligatoires pour tout controller/service. `SamplingsController` (7 endpoints terrain, cœur du métier préleveur), `PermissionService` (cœur de l'autorisation) et `UserManagementService` (gestion des comptes) n'ont **aucun** test. Les conventions exigent aussi de tester routes/attributs HTTP/auth — impossible sur ces classes.
- Snippet :
  ```text
  Controllers testés : 18/20  (manquants : SamplingsController, LogsController)
  Services testés    : 16/20  (manquants : UserManagementService, PermissionService,
                               OrderAuditService, TokenService)
  ```
- Proposition : compléter en priorité `PermissionService` et `SamplingsController` (auth attributes + happy/sad paths).

### F-224 — Tests Infrastructure 100% EF InMemory : divergences PostgreSQL non couvertes
- Fichier : `tests/AquaPlan.Infrastructure.Tests/**` (23 fichiers `UseInMemoryDatabase`)
- Sévérité : Medium — Effort : XL
- Raison : risque que CLAUDE.md signale lui-même. Exemples concrets non couverts : l'index filtré `HasFilter("sample_barcode IS NOT NULL")` (SamplingConfiguration) est ignoré par InMemory ; les comparaisons `ToLower().Contains` et les sous-requêtes corrélées de `GetOrdersFilteredAsync` ne valident pas leur traduction SQL ; les transactions sont bypassées (cf. F-225).
- Snippet :
  ```csharp
  var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
      .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
      .Options;
  ```
- Proposition : introduire Testcontainers-PostgreSQL pour une suite d'intégration ciblée (requêtes complexes + contraintes), garder InMemory pour la logique pure.

### F-225 — Détection du provider InMemory dans le code de production
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingPlanService.cs:381-384`
- Sévérité : Medium — Effort : S
- Raison : la branche de test fuit dans le code métier : la transaction de `GenerateOrdersFromPlanAsync` est désactivée quand le provider est InMemory. Le chemin réellement exécuté en production (transactionnel) n'est donc **jamais** celui testé, et le code prod porte une dépendance conceptuelle au harnais de test.
- Snippet :
  ```csharp
  var supportsTransactions = dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory";
  var transaction = supportsTransactions
      ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
      : null;
  ```
- Proposition : utiliser `dbContext.Database.CreateExecutionStrategy()` + transaction inconditionnelle, et tester ce chemin avec Testcontainers (cf. F-224).

### F-226 — `GetPreleveurs` : matching de rôle par sous-chaîne accentuée
- Fichier : `src/AquaPlan.Api/Controllers/Api/UsersController.cs:38-42`
- Sévérité : Medium — Effort : S
- Raison : le filtre repose sur `Contains("réleveur")` pour attraper "Préleveur" et "Requérant-Préleveur" — fragile (tout renommage/ajout de rôle casse silencieusement la liste d'assignation), exécuté en mémoire après le N+1 de F-209, et la logique métier vit dans le controller.
- Snippet :
  ```csharp
  var preleveurs = users.Where(u =>
      u.Role != null && (
          u.Role.Contains("réleveur", StringComparison.OrdinalIgnoreCase) ||
          u.Role.Contains("releveur", StringComparison.OrdinalIgnoreCase)
      )).ToList();
  ```
- Proposition : filtrer sur l'égalité avec `RoleName.Preleveur` / `RoleName.RequerantPreleveur` via la table UserRoles, dans le service.

### F-227 — Logique « accès distributeur » implémentée 4 fois différemment
- Fichier : `src/AquaPlan.Infrastructure/Services/DelegationService.cs:108-126`, `OrderService.cs:549-581`, `SamplingPlanService.cs:328-351`, `SamplingLocationChangeRequestService.cs:220-229`
- Sévérité : Medium — Effort : M
- Raison : duplication > 30 lignes avec des sémantiques divergentes : `DelegationService` inclut le distributeur primaire ; `SamplingPlanService.UserHasDistributorAccessAsync` ne l'inclut pas ; `OrderService.UserHasDistributorAccessAsync` non plus ; la version ChangeRequest ignore en plus les délégations (F-113). Toute correction (cf. "Bug fix" AQ-369) doit être reportée 4 fois — et ne l'a pas été.
- Snippet :
  ```csharp
  // SamplingPlanService.cs:332 — ignore AppUser.DistributorId (lien primaire)
  var hasDirectAccess = await dbContext.UserDistributors
      .AnyAsync(ud => ud.UserId == userId && ud.DistributorId == distributorId, cancellationToken);
  ```
- Proposition : exposer `IDelegationService.UserHasDistributorAccessAsync` comme unique source de vérité et supprimer les 3 copies.

### F-228 — `CreateOrderAsync` (chemin admin) : références FK non validées contre le tenant
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:221-282`
- Sévérité : Medium — Effort : S
- Raison : pour un admin, ni `dto.DistributorId`, ni `dto.SamplingLocationId`, ni `dto.AnalysisProgramIds` ne sont vérifiés comme appartenant au tenant courant (le check délégation du controller ne s'applique qu'aux non-admins). Un mandat du tenant A peut référencer le distributeur/LDP/programme du tenant B — incohérence référentielle inter-tenant qui fuit ensuite dans les DTOs (noms d'entités d'un autre tenant).
- Snippet :
  ```csharp
  var order = new Order
  {
      ...
      DistributorId = dto.DistributorId,            // ← jamais validé vs tenantId
      SamplingLocationId = dto.SamplingLocationId,  // ← idem (+ appartenance au distributeur)
      TenantId = tenantId,
  ```
- Proposition : valider en une requête que distributeur/LDP/programmes existent dans `tenantId` (et que le LDP appartient au distributeur).

### F-229 — Refresh tokens en clair + session unique par utilisateur
- Fichier : `src/AquaPlan.Domain/Entities/AppUser.cs:18-19` + `src/AquaPlan.Application/Services/AuthService.cs:38-40, 49`
- Sévérité : Medium — Effort : M
- Raison : (1) le refresh token est stocké en clair dans `AspNetUsers` — un dump DB donne 7 jours d'accès par compte ; (2) un seul token par utilisateur : chaque login/refresh invalide la session des autres appareils (tablette terrain vs poste bureau) — source de déconnexions inexpliquées ; (3) `userManager.Users.FirstOrDefault(...)` est une requête **synchrone** bloquante.
- Snippet :
  ```csharp
  public async Task<LoginResponseDto?> RefreshTokenAsync(string refreshToken, ...)
  {
      var user = userManager.Users.FirstOrDefault(u => u.RefreshToken == refreshToken);  // sync + token en clair
  ```
- Proposition : stocker un hash SHA-256 du token (lookup par hash, `FirstOrDefaultAsync`) ; à terme, table `RefreshTokens` multi-sessions avec rotation.

### F-230 — Migration `RestructureStatusWorkflow` : Down() irréversible (perte d'information)
- Fichier : `src/AquaPlan.Infrastructure/Data/Migrations/20260413182840_RestructureStatusWorkflow.cs:43-50`
- Sévérité : Medium — Effort : S
- Raison : le Up() fusionne plusieurs anciens statuts vers un seul (`-6` ET `-5` → `2` ; `-3` ET `-2` → `4`). Le Down() ne peut donc pas restituer les valeurs d'origine : un rollback de cette migration corrompt les statuts historiques. Risque dormant tant que la migration reste dans l'historique déployable.
- Snippet :
  ```csharp
  migrationBuilder.Sql("UPDATE orders SET status = 2 WHERE status = -6;");   // SamplingCompleted → Completed
  migrationBuilder.Sql("UPDATE orders SET status = 2 WHERE status = -5;");   // Validated → Completed (fusion)
  migrationBuilder.Sql("UPDATE orders SET status = 4 WHERE status = -3;");   // ResultsReceived → Done
  migrationBuilder.Sql("UPDATE orders SET status = 4 WHERE status = -2;");   // Completed → Done (fusion)
  ```
- Proposition : faire lever `NotSupportedException` dans `Down()` avec un message explicite plutôt qu'un rollback silencieusement destructif.

---

## Low

### F-301 — `ILogger` injecté mais jamais utilisé dans 12 controllers
- Fichier : `src/AquaPlan.Api/Controllers/Api/` — OrdersController, SamplingRoundsController, SamplingPlansController, SamplingsController, SamplingLocationsController, SamplingLocationChangeRequestsController, UsersController, RolesController, DelegationsController, DistributorsController, SectorsController, AnalysisProfiles/ProgramsController
- Sévérité : Low — Effort : S
- Raison : dépendance morte dans les constructeurs primaires (warning potentiel, bruit DI, signature de test gonflée — les tests mockent un logger inutile).
- Snippet :
  ```csharp
  public class OrdersController(
      IOrderService orderService, ..., 
      ILogger<OrdersController> logger) : ControllerBase   // ← logger jamais référencé
  ```
- Proposition : supprimer le paramètre (et l'argument correspondant dans les tests).

### F-302 — `GetUserId()` / `GetTenantId()` copiés-collés dans 13 controllers
- Fichier : tous les controllers (ex. `OrdersController.cs:381-402`, `SamplingRoundsController.cs:264-273`, …)
- Sévérité : Low — Effort : S
- Raison : ~20 lignes dupliquées par controller ; `Guid.Parse` sans `TryParse` (claim malformée → 500 au lieu de 401). Le candidat classique à une classe de base `AquaPlanControllerBase`.
- Snippet :
  ```csharp
  private Guid GetTenantId()
  {
      var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
      return Guid.Parse(tenantClaim);
  }
  ```
- Proposition : classe de base commune avec `UserId`/`TenantId` (et `Guid.TryParse` → 401 propre).

### F-303 — Littéraux magiques `"Administrator"` / `"ViewAllOrders"` au lieu des constantes
- Fichier : `MockLimsController.cs:15`, `RolesController.cs:10`, `UsersController.cs:18`, `DistributorsController.cs:45`, `SectorsController.cs:46`, `SamplingLocationsController.cs:71,77`, etc. ; `"ViewAllOrders"` en littéral dans ~15 actions
- Sévérité : Low — Effort : S
- Raison : `RoleName.Administrator` et `PermissionName.ViewAllOrders` existent mais la moitié des controllers utilisent la chaîne brute — un renommage casserait silencieusement les autorisations (violation de la règle `nameof`/constantes de CLAUDE.md).
- Snippet :
  ```csharp
  [Authorize(Roles = "Administrator")]      // SamplingLocationsController.cs:77
  ...
  [Authorize(Roles = RoleName.Administrator)]   // AdminLimsSyncController.cs:17 (forme correcte)
  ```
- Proposition : remplacement mécanique par les constantes `RoleName.*` / `PermissionName.*`.

### F-304 — `Include` morts devant des projections `Select`
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:138-141`, `SamplingPlanService.cs:80-81`
- Sévérité : Low — Effort : XS
- Raison : EF Core ignore les `Include` quand la requête se termine par une projection — quatre `Include` inutiles qui suggèrent à tort un chargement de graphe et alourdissent la lecture.
- Snippet :
  ```csharp
  var rows = await query
      .Skip(...).Take(...)
      .Include(o => o.CreatedBy)        // ← ignoré : la requête projette ensuite
      .Include(o => o.Preleveur)
      ...
      .Select(o => new { ... })
  ```
- Proposition : supprimer les `Include` redondants.

### F-305 — Cast `as string` redondant sur `MaxAsync`
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:928-931`
- Sévérité : Low — Effort : XS
- Raison : `Select(o => o.OrderNumber).MaxAsync()` renvoie déjà `string?` ; le `as string` est un bruit qui masque l'intention.
- Snippet :
  ```csharp
  var maxNumber = await dbContext.Orders
      .Where(o => o.OrderNumber.StartsWith(prefix))
      .Select(o => o.OrderNumber)
      .MaxAsync(cancellationToken) as string;   // ← cast inutile
  ```
- Proposition : supprimer le cast.

### F-306 — Numérotation des mandats non scopée par tenant
- Fichier : `src/AquaPlan.Infrastructure/Services/OrderService.cs:924-931`
- Sévérité : Low — Effort : XS
- Raison : la séquence `ORD-yyyyMMdd-xxxx` est globale : un tenant observe les volumes de commande des autres (fuite d'information faible) et la contention de la race F-110 est inter-tenant.
- Snippet :
  ```csharp
  var maxNumber = await dbContext.Orders
      .Where(o => o.OrderNumber.StartsWith(prefix))   // ← pas de o.TenantId == tenantId
  ```
- Proposition : ajouter le filtre tenant (ou préfixe par tenant) lors du passage à une séquence (F-110).

### F-307 — Endpoints `/api/orders/...` déclarés dans `SamplingRoundsController`
- Fichier : `src/AquaPlan.Api/Controllers/Api/SamplingRoundsController.cs:231-262`
- Sévérité : Low — Effort : S
- Raison : `replace-location`, `start`, `sampler-comment` sont des actions sur les **orders**, montées via la syntaxe `~/` dans le controller des tournées — introuvables quand on cherche dans OrdersController, et NSwag les regroupe sous le mauvais tag.
- Snippet :
  ```csharp
  [HttpPost("~/api/orders/{orderId:guid}/replace-location")]
  public async Task<ActionResult> ReplaceLocation(...)
  ```
- Proposition : déplacer ces trois actions dans `OrdersController` (mêmes services injectés).

### F-308 — `AuthService` : `cancellationToken` ignoré partout
- Fichier : `src/AquaPlan.Application/Services/AuthService.cs:16-125`
- Sévérité : Low — Effort : XS
- Raison : toutes les signatures acceptent un `CancellationToken` jamais transmis (les API `UserManager` utilisées n'en prennent pas, mais `userManager.Users.FirstOrDefault` pourrait être `FirstOrDefaultAsync(ct)`) — fausse promesse d'annulation.
- Snippet :
  ```csharp
  public async Task<LoginResponseDto?> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
  {
      var user = await userManager.FindByEmailAsync(dto.Email);   // ct ignoré
  ```
- Proposition : utiliser les surcharges async EF avec `ct` quand disponibles ; sinon supprimer le paramètre des signatures.

### F-309 — `CompleteAsync` charge le graphe programmes/profils sans l'utiliser
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingService.cs:187-196`
- Sévérité : Low — Effort : XS
- Raison : l'Include 4 niveaux (`OrderAnalysisPrograms → … → AnalysisProfile`) n'est référencé nulle part dans la méthode (probable reliquat d'une ancienne validation des containers requis) — coût SQL gratuit sur le chemin terrain le plus fréquent.
- Snippet :
  ```csharp
  var order = await dbContext.Orders
      .Include(o => o.Sampling).ThenInclude(s => s!.Containers)
      .Include(o => o.SamplingRound)
      .Include(o => o.OrderAnalysisPrograms)              // ← jamais utilisé dans CompleteAsync
          .ThenInclude(oap => oap.AnalysisProgram!)...
  ```
- Proposition : supprimer cet Include.

### F-310 — Conventions CLAUDE.md non implémentées : `[ApiVersion]`, `[Audited]`, `[FeatureGate]`
- Fichier : ensemble de `src/AquaPlan.Api/Controllers/Api/` (aucune occurrence)
- Sévérité : Low — Effort : M
- Raison : CLAUDE.md impose `[ApiVersion("1.0")]` sur les controllers, `[Audited]` sur les endpoints sensibles et `[FeatureGate]` pour les features conditionnelles — aucun des trois attributs n'existe dans le code (le Mock LIMS est gaté par un `if (!options.Value.Enabled)` manuel). Documentation et code divergent : soit implémenter, soit amender CLAUDE.md.
- Snippet :
  ```csharp
  // MockLimsController.cs — gating manuel au lieu de [FeatureGate]
  if (!options.Value.Enabled) { return NotFound(); }
  ```
- Proposition : trancher (implémenter l'audit par attribut + versioning, ou retirer ces règles de CLAUDE.md).

### F-311 — CORS codé en dur sur `http://localhost:4200`
- Fichier : `src/AquaPlan.Api/Program.cs:100-109`
- Sévérité : Low — Effort : XS
- Raison : l'origine autorisée n'est pas configurable par environnement. Inoffensif en prod (SPA servie même origine) mais bloque tout déploiement où le front est sur un autre host, et `AllowCredentials()` + origine fixe dev dans le binaire prod est de l'hygiène douteuse.
- Snippet :
  ```csharp
  policy.WithOrigins("http://localhost:4200")
      .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
  ```
- Proposition : lire les origines depuis `Configuration.GetSection("Cors:AllowedOrigins")`.

### F-312 — `GetRequiredContainers` : requête lourde exécutée avant le contrôle d'accès
- Fichier : `src/AquaPlan.Api/Controllers/Api/OrdersController.cs:245-268`
- Sévérité : Low — Effort : XS
- Raison : le graphe complet programmes/profils/containers est chargé **avant** la vérification `UserCanAccessOrderAsync` — coût payé même pour un appelant non autorisé (et ordre inverse des autres endpoints).
- Snippet :
  ```csharp
  var containers = await orderService.GetRequiredContainersAsync(id, tenantId, cancellationToken);
  if (containers is null) return NotFound();
  var hasViewAll = await permissionService.UserHasPermissionAsync(...);   // ← après le fetch
  ```
- Proposition : inverser : authz d'abord, fetch ensuite.

### F-313 — Idempotence Mock LIMS sujette à une race (contrainte unique → 500)
- Fichier : `src/AquaPlan.Infrastructure/Services/MockLims/MockLimsService.cs:33-56`
- Sévérité : Low — Effort : S
- Raison : le check `existing` puis insert n'est pas atomique ; deux transmissions concurrentes du même `OrderReference` violent l'index unique `(TenantId, OrderReference)` → `DbUpdateException` au lieu du retour idempotent voulu. Best-effort côté appelant (log + skip), mais le mandat reste alors sans `LimsOrderId`.
- Snippet :
  ```csharp
  var existing = await dbContext.MockLimsOrders
      .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.OrderReference == dto.OrderReference, ...);
  if (existing is not null) return new MockLimsOrderCreatedDto(existing.LimsOrderId, existing.ReceivedAt);
  ...
  dbContext.MockLimsOrders.Add(entity);   // ← race avec l'index unique
  ```
- Proposition : catcher la violation d'unicité et relire la ligne existante (retry-on-conflict).

### F-314 — `DistributorsController.GetById` non scopé pour les non-admins
- Fichier : `src/AquaPlan.Api/Controllers/Api/DistributorsController.cs:32-42`
- Sévérité : Low — Effort : XS
- Raison : `GetAll` applique le scoping AQ-419 (distributeurs autorisés seulement), mais `GetById` renvoie n'importe quel distributeur du tenant — incohérence mineure de visibilité (données peu sensibles : nom, région).
- Snippet :
  ```csharp
  [HttpGet("{id:guid}")]
  public async Task<ActionResult<DistributorDto>> GetById(Guid id, ...)
  {
      var distributor = await distributorService.GetByIdAsync(id, tenantId, cancellationToken);  // pas de check AQ-419
  ```
- Proposition : appliquer le même filtre d'autorisation que `GetAll` (ou documenter l'exception).

### F-315 — Sync LIMS sans retry/backoff exponentiel (exigence CLAUDE.md)
- Fichier : `src/AquaPlan.Infrastructure/HostedServices/MockLimsResultsSyncWorker.cs:48-80`
- Sévérité : Low — Effort : M
- Raison : CLAUDE.md : "All LIMS operations must be idempotent and resilient (retry with exponential backoff)". Le worker boucle à intervalle fixe et compte les échecs consécutifs sans backoff ; les pushes outbound (`TransmitOrdersToMockLimsAsync`) sont swallowed sans retry. Acceptable pour le mock, bloquant pour Limsophy réel.
- Snippet :
  ```csharp
  catch (Exception ex)
  {
      _consecutiveErrorCycles++;
      logger.LogError(ex, "...cycle failed (consecutive errors={Count})", _consecutiveErrorCycles);
      // ← pas de backoff, intervalle fixe
  }
  ```
- Proposition : Polly (retry exponentiel + circuit breaker) autour des appels `ILimsResultService`/futur `ILimsophyClient`.

### F-316 — Commentaire faux dans `ToggleStatusAsync` : « aucune FK Order → SamplingLocation »
- Fichier : `src/AquaPlan.Infrastructure/Services/SamplingLocationService.cs:200-205`
- Sévérité : Low — Effort : XS
- Raison : la FK existe bel et bien (`OrderConfiguration.cs:35-38`, `Order.SamplingLocationId`) et `DeleteAsync` la vérifie déjà. Le commentaire trompeur fige `hasActiveReferences = false` : la désactivation d'un LDP référencé par des mandats actifs ne produit jamais l'avertissement prévu.
- Snippet :
  ```csharp
  // Warning placeholder: when SamplingLocation is referenced by active orders/samplings,
  // add a warning message here. Currently no FK exists from Order/Sampling to SamplingLocation.
  var hasActiveReferences = false;       // ← faux : la FK existe (orders.sampling_location_id)
  string? warning = null;
  ```
- Proposition : implémenter le check (`Orders.AnyAsync(o => o.SamplingLocationId == id && statut actif)`) et corriger le commentaire.

### F-317 — Seeders et migrations exécutés à chaque démarrage, sans distinction d'environnement
- Fichier : `src/AquaPlan.Api/Program.cs:148-159`
- Sévérité : Low — Effort : S
- Raison : `MigrateAsync` + 2 seeders à chaque boot de chaque réplica : en K8S multi-pods (cible prod déclarée), deux pods démarrant simultanément peuvent exécuter migrations/seeds en concurrence (verrous DDL, doublons potentiels dans les seeds non idempotents par rapport aux races). Couple aussi le démarrage de l'API à des écritures DB.
- Snippet :
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      var db = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
      await db.Database.MigrateAsync();
  }
  await RoleAndPermissionSeeder.SeedAsync(app.Services);
  ```
- Proposition : job de migration dédié (init container / `dotnet ef database update` en CI) ; seeds gardés par environnement.

---

## Patterns récurrents

1. **Autorisation « tenant = sécurité »** — Le filtrage `TenantId` est systématique et bien fait (bon point), mais il est trop souvent la *seule* barrière : une dizaine d'endpoints d'écriture (assign, transition, cancel, delete round, validate sampling, submit plan…) sont accessibles à **tout utilisateur authentifié du tenant**, alors que le modèle métier distingue clairement admin / mandataire / préleveur. Les protections fines (AQ-369, AQ-398, AQ-420) ont été ajoutées sprint après sprint sur les endpoints de *lecture/liste*, mais pas reportées sur les écritures unitaires. → Recommandation : inventaire systématique des endpoints d'écriture + policy handler central (`IAuthorizationHandler` distributeur).

2. **Deux machines à états Order qui divergent** — Les transitions de statut existent en 4 exemplaires (OrderStatusService unitaire, OrderService bulk, SamplingRoundService.TransmitAll, SamplingService.Complete) avec des effets de bord inconsistants : push LIMS et `TransmittedAt` seulement dans le bulk (F-105, F-108), audit log absent du chemin tournée, libération du lock seulement dans TransmitAll (F-215). C'est la cause directe du hotfix AQ-404 (backfill). → Recommandation : un unique `OrderTransitionService` portant transitions + effets de bord, consommé par tous les chemins.

3. **Isolation tenant percée par les IDs client** — Trois endpoints acceptent un identifiant de scope venant du client sans le confronter au token (`UserCreateDto.TenantId` F-004, `MockLimsBackfillRequestDto.TenantId` F-003, `DistributorId` non validés F-112/F-219/F-228). Le réflexe « le tenant vient TOUJOURS de la claim, jamais du body » n'est pas ancré. → Recommandation : supprimer tout champ TenantId des DTOs d'entrée ; revue grep `dto.TenantId|request.TenantId`.

4. **Secrets de développement qui fuient vers la prod** — Fallbacks codés en dur dans Program.cs, appsettings commis, defaults `:-` dans le compose Synology, admin seedé partout (F-001, F-002). Le pattern « valeur par défaut pratique en dev » est appliqué à des secrets. → Recommandation : fail-fast sur tout secret manquant + scan de secrets en CI.

5. **Read-modify-write sans protection concurrente** — Numéro de mandat (F-110), UserNumber (F-217), lock de tournée (F-216), idempotence MockLims (F-313) : même motif « lire, tester, écrire » sans séquence, contrainte exploitée ou token de concurrence. L'app est offline-first avec resync — la concurrence n'est pas théorique. → Recommandation : séquences PostgreSQL + `UseXminAsConcurrencyToken` sur les agrégats verrouillables.

6. **Logique d'accès distributeur dupliquée et désynchronisée** — 4 implémentations dont 2 ont déjà divergé après le bug fix AQ-369 (F-113, F-227). Le « rule of three » de CLAUDE.md est dépassé. → Recommandation : centraliser dans `IDelegationService` et supprimer les copies.

7. **Doubles pipelines morts ou redondants** — Filtre d'exception jamais enregistré mais testé (F-201), projet Worker stub (F-222), méthodes de service sans appelant (F-222), Includes morts (F-304), logger non utilisé dans 12 controllers (F-301). Symptôme d'une vélocité de sprint élevée sans passes de nettoyage. → Recommandation : passe `dotnet format analyzers` + règle CA1801/IDE0060 en erreur.

8. **Tests nombreux mais aveugles au vrai moteur SQL** — 100% InMemory (F-224), branche InMemory dans le code prod (F-225), aucune couverture des contraintes (index filtrés, unicité, transactions) ni des briques d'autorisation centrales (PermissionService non testé, F-223). La discipline FluentAssertions/naming est, elle, bien respectée (aucun `Assert.*`, aucun test skippé). → Recommandation : suite Testcontainers ciblée sur les requêtes complexes + tests d'autorisation de controller systématiques (les attributs `[Authorize]` manquants de F-104/F-106 auraient été attrapés par les tests d'attributs exigés par CLAUDE.md).

9. **Validation d'entrée à deux vitesses** — Les DTOs récents (SamplingRounds, AQ-37x) sont annotés proprement ; les DTOs historiques (Orders, Users, Sectors, Distributors…) n'ont aucune annotation (F-220), reportant la validation sur les contraintes EF (→ 500) ou sur rien du tout. → Recommandation : aligner toutes les longueurs sur les `HasMaxLength` EF, `[Required]` sur les champs métier obligatoires.

10. **Messages d'exception internes renvoyés au client** — Le mapping générique `InvalidOperationException/UnauthorizedAccessException → message brut` (F-202, F-203) transforme chaque message de debug en réponse API. Avec la règle CLAUDE.md « never expose internal details », il faut une hiérarchie d'exceptions métier explicite (déjà amorcée avec `ConflictOperationException`/`ForbiddenOperationException` — il reste à finir la migration).
