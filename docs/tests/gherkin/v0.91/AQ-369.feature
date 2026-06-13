Feature: AQ-369 Creation mandat/tournee restreinte au distributeur autorise

  Regle metier : un Requerant (ou Requerant-Preleveur) ne peut creer
  un mandat ou une tournee que sur son propre distributeur ou sur
  un distributeur qui lui a delegue l autorite via une delegation
  active (IsActive=true, ValidFrom <= now <= ValidTo). Admin n est
  pas restreint. Isolation tenant preservee.

  Scenario: Requerant avec delegation active voit ses distributeurs autorises
    Etant donne un Requerant U1 rattache au distributeur D1
    Et une DistributorDelegation active D2 vers D1 (IsActive=true, ValidFrom <= now <= ValidTo)
    Quand U1 appelle GET /api/delegations/my-authorized-distributors
    Alors la reponse est 200 OK
    Et la liste contient D1 et D2
    Et un POST /api/orders avec distributorId=D1 retourne 201
    Et un POST /api/orders avec distributorId=D2 retourne 201

  Scenario: Requerant tente de creer sur un distributeur non autorise
    Etant donne un Requerant U1 rattache a D1
    Et une DistributorDelegation D2 vers D1 expiree (ValidTo dans le passe)
    Quand U1 appelle POST /api/sampling-rounds avec distributorId=D2
    Alors la reponse est 403 Forbidden
    Et le body contient error="distributor.notAuthorized"
    Et aucune entite n est persistee

  Scenario: Admin non restreint et isolation tenant
    Etant donne un Admin connecte au tenant T1 avec 5 distributeurs
    Quand il appelle GET /api/delegations/my-authorized-distributors
    Alors la reponse est 200 OK avec les 5 distributeurs de T1
    Et POST /api/orders sur n importe lequel retourne 201
    Et un Admin du tenant T2 ne voit jamais les distributeurs de T1
    Et un Preleveur (non-Requerant) reste a 403 Forbidden au POST /api/orders
