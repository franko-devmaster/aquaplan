using AquaPlan.Application.DTOs.ChangeRequests;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class SamplingLocationChangeRequestServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<SamplingLocationChangeRequestService>> _loggerMock = new();
    private readonly SamplingLocationChangeRequestService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";
    private const string ReviewerId = "reviewer-1";

    public SamplingLocationChangeRequestServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new SamplingLocationChangeRequestService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region SubmitCreateRequestAsync

    [Fact]
    public async Task SubmitCreateRequestAsync_ShouldCreatePendingRequest()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source Neuve", "SN-001", 46.8, 7.15, "Description", DistributorId);

        var result = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);

        result.Should().NotBeNull();
        result.RequestType.Should().Be(ChangeRequestType.Create);
        result.Status.Should().Be(ChangeRequestStatus.Pending);
        result.ProposedName.Should().Be("Source Neuve");
        result.ProposedLocationCode.Should().Be("SN-001");
        result.ProposedLatitude.Should().Be(46.8);
        result.ProposedLongitude.Should().Be(7.15);
        result.ProposedDescription.Should().Be("Description");
        result.DistributorId.Should().Be(DistributorId);
        result.RequestedById.Should().Be(UserId);
    }

    [Fact]
    public async Task SubmitCreateRequestAsync_ShouldPersistRequest()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source Neuve", "SN-001", null, null, null, DistributorId);

        var result = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);

        var persisted = await _dbContext.SamplingLocationChangeRequests.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(TenantId);
        persisted.RequestType.Should().Be(ChangeRequestType.Create);
        persisted.Status.Should().Be(ChangeRequestStatus.Pending);
    }

    [Fact]
    public async Task SubmitCreateRequestAsync_WhenNoDistributorAccess_ShouldThrowUnauthorizedAccessException()
    {
        await SeedDistributor();

        var dto = new ChangeRequestCreateDto("Source Neuve", "SN-001", null, null, null, DistributorId);

        await _sut.Invoking(x => x.SubmitCreateRequestAsync(dto, UserId, TenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage($"*{UserId}*{DistributorId}*");
    }

    #endregion

    #region SubmitUpdateRequestAsync

    [Fact]
    public async Task SubmitUpdateRequestAsync_ShouldCreatePendingUpdateRequest()
    {
        var location = await SeedSamplingLocation();
        var dto = new ChangeRequestUpdateDto("Nom Modifie", "NM-001", 46.9, 7.2, "Nouvelle description");

        var result = await _sut.SubmitUpdateRequestAsync(location.Id, dto, UserId, TenantId);

        result.Should().NotBeNull();
        result.RequestType.Should().Be(ChangeRequestType.Update);
        result.Status.Should().Be(ChangeRequestStatus.Pending);
        result.SamplingLocationId.Should().Be(location.Id);
        result.ProposedName.Should().Be("Nom Modifie");
        result.ProposedLocationCode.Should().Be("NM-001");
        result.ProposedLatitude.Should().Be(46.9);
        result.ProposedLongitude.Should().Be(7.2);
        result.ProposedDescription.Should().Be("Nouvelle description");
    }

    [Fact]
    public async Task SubmitUpdateRequestAsync_WhenLocationNotFound_ShouldThrowKeyNotFoundException()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestUpdateDto("Nom", "CODE", null, null, null);

        await _sut.Invoking(x => x.SubmitUpdateRequestAsync(Guid.NewGuid(), dto, UserId, TenantId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SubmitUpdateRequestAsync_WhenNoDistributorAccess_ShouldThrowUnauthorizedAccessException()
    {
        await SeedDistributor();
        var location = await SeedSamplingLocationEntity();
        var dto = new ChangeRequestUpdateDto("Nom", "CODE", null, null, null);

        await _sut.Invoking(x => x.SubmitUpdateRequestAsync(location.Id, dto, UserId, TenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region SubmitDeactivateRequestAsync

    [Fact]
    public async Task SubmitDeactivateRequestAsync_ShouldCreatePendingDeactivateRequest()
    {
        var location = await SeedSamplingLocation();

        var result = await _sut.SubmitDeactivateRequestAsync(location.Id, UserId, TenantId);

        result.Should().NotBeNull();
        result.RequestType.Should().Be(ChangeRequestType.Deactivate);
        result.Status.Should().Be(ChangeRequestStatus.Pending);
        result.SamplingLocationId.Should().Be(location.Id);
    }

    [Fact]
    public async Task SubmitDeactivateRequestAsync_WhenLocationNotFound_ShouldThrowKeyNotFoundException()
    {
        await SeedDistributorWithUserAccess();

        await _sut.Invoking(x => x.SubmitDeactivateRequestAsync(Guid.NewGuid(), UserId, TenantId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SubmitDeactivateRequestAsync_WhenNoDistributorAccess_ShouldThrowUnauthorizedAccessException()
    {
        await SeedDistributor();
        var location = await SeedSamplingLocationEntity();

        await _sut.Invoking(x => x.SubmitDeactivateRequestAsync(location.Id, UserId, TenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region GetMyRequestsAsync

    [Fact]
    public async Task GetMyRequestsAsync_ShouldReturnOnlyUserRequests()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequests();

        var result = await _sut.GetMyRequestsAsync(UserId, TenantId);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.RequestedById.Should().Be(UserId));
    }

    [Fact]
    public async Task GetMyRequestsAsync_ShouldReturnOrderedByRequestedAtDescending()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequests();

        var result = await _sut.GetMyRequestsAsync(UserId, TenantId);

        result.Should().BeInDescendingOrder(r => r.RequestedAt);
    }

    [Fact]
    public async Task GetMyRequestsAsync_ShouldNotReturnOtherUsersRequests()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequests();

        var result = await _sut.GetMyRequestsAsync("non-existent-user", TenantId);

        result.Should().BeEmpty();
    }

    #endregion

    #region GetPendingRequestsAsync

    [Fact]
    public async Task GetPendingRequestsAsync_ShouldReturnOnlyPendingRequestsForTenant()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequests();

        var result = await _sut.GetPendingRequestsAsync(TenantId);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.Status.Should().Be(ChangeRequestStatus.Pending));
    }

    [Fact]
    public async Task GetPendingRequestsAsync_ShouldReturnOrderedByRequestedAtAscending()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequests();

        var result = await _sut.GetPendingRequestsAsync(TenantId);

        result.Should().BeInAscendingOrder(r => r.RequestedAt);
    }

    [Fact]
    public async Task GetPendingRequestsAsync_ShouldNotReturnApprovedOrRejectedRequests()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequests();

        var result = await _sut.GetPendingRequestsAsync(TenantId);

        result.Should().NotContain(r => r.Status == ChangeRequestStatus.Approved);
        result.Should().NotContain(r => r.Status == ChangeRequestStatus.Rejected);
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_ShouldReturnRequest_WhenExists()
    {
        await SeedDistributorWithUserAccess();
        var requests = await SeedChangeRequests();

        var result = await _sut.GetByIdAsync(requests[0].Id, TenantId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(requests[0].Id);
        result.RequestType.Should().Be(ChangeRequestType.Create);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenDifferentTenant()
    {
        await SeedDistributorWithUserAccess();
        var requests = await SeedChangeRequests();

        var result = await _sut.GetByIdAsync(requests[0].Id, OtherTenantId);

        result.Should().BeNull();
    }

    #endregion

    #region ApproveAsync

    [Fact]
    public async Task ApproveAsync_WhenCreateRequest_ShouldCreateNewSamplingLocation()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source Neuve", "SN-001", 46.8, 7.15, "Description source", DistributorId);
        var submitted = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);

        var result = await _sut.ApproveAsync(submitted.Id, "Approuve", ReviewerId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(ChangeRequestStatus.Approved);
        result.ReviewedById.Should().Be(ReviewerId);
        result.ReviewComment.Should().Be("Approuve");
        result.ReviewedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.SamplingLocationId.Should().NotBeNull();

        var location = await _dbContext.SamplingLocations.FirstOrDefaultAsync(sl => sl.Id == result.SamplingLocationId);
        location.Should().NotBeNull();
        location!.Name.Should().Be("Source Neuve");
        location.LocationCode.Should().Be("SN-001");
        location.Latitude.Should().Be(46.8);
        location.Longitude.Should().Be(7.15);
        location.Description.Should().Be("Description source");
        location.DistributorId.Should().Be(DistributorId);
        location.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAsync_WhenUpdateRequest_ShouldUpdateExistingSamplingLocation()
    {
        var location = await SeedSamplingLocation();
        var dto = new ChangeRequestUpdateDto("Nom Modifie", "NM-001", 46.9, 7.2, "Nouvelle description");
        var submitted = await _sut.SubmitUpdateRequestAsync(location.Id, dto, UserId, TenantId);

        var result = await _sut.ApproveAsync(submitted.Id, "OK", ReviewerId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(ChangeRequestStatus.Approved);

        var updatedLocation = await _dbContext.SamplingLocations.FindAsync(location.Id);
        updatedLocation.Should().NotBeNull();
        updatedLocation!.Name.Should().Be("Nom Modifie");
        updatedLocation.LocationCode.Should().Be("NM-001");
        updatedLocation.Latitude.Should().Be(46.9);
        updatedLocation.Longitude.Should().Be(7.2);
        updatedLocation.Description.Should().Be("Nouvelle description");
    }

    [Fact]
    public async Task ApproveAsync_WhenDeactivateRequest_ShouldDeactivateSamplingLocation()
    {
        var location = await SeedSamplingLocation();
        var submitted = await _sut.SubmitDeactivateRequestAsync(location.Id, UserId, TenantId);

        var result = await _sut.ApproveAsync(submitted.Id, null, ReviewerId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(ChangeRequestStatus.Approved);

        var deactivatedLocation = await _dbContext.SamplingLocations.FindAsync(location.Id);
        deactivatedLocation.Should().NotBeNull();
        deactivatedLocation!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestNotFound_ShouldReturnNull()
    {
        var result = await _sut.ApproveAsync(Guid.NewGuid(), "Comment", ReviewerId, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestAlreadyApproved_ShouldReturnNull()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source", "S-001", null, null, null, DistributorId);
        var submitted = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);
        await _sut.ApproveAsync(submitted.Id, null, ReviewerId, TenantId);

        var result = await _sut.ApproveAsync(submitted.Id, "Second approval", ReviewerId, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_WhenRequestAlreadyRejected_ShouldReturnNull()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source", "S-001", null, null, null, DistributorId);
        var submitted = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);
        await _sut.RejectAsync(submitted.Id, "Refuse", ReviewerId, TenantId);

        var result = await _sut.ApproveAsync(submitted.Id, "Try approve", ReviewerId, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source", "S-001", null, null, null, DistributorId);
        var submitted = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);

        var result = await _sut.ApproveAsync(submitted.Id, null, ReviewerId, OtherTenantId);

        result.Should().BeNull();
    }

    #endregion

    #region RejectAsync

    [Fact]
    public async Task RejectAsync_ShouldSetStatusToRejectedWithComment()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source", "S-001", null, null, null, DistributorId);
        var submitted = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);

        var result = await _sut.RejectAsync(submitted.Id, "Localisation incorrecte", ReviewerId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(ChangeRequestStatus.Rejected);
        result.ReviewedById.Should().Be(ReviewerId);
        result.ReviewComment.Should().Be("Localisation incorrecte");
        result.ReviewedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RejectAsync_ShouldNotAffectSamplingLocation()
    {
        var location = await SeedSamplingLocation();
        var dto = new ChangeRequestUpdateDto("Nom Modifie", "NM-001", 46.9, 7.2, "Nouvelle description");
        var submitted = await _sut.SubmitUpdateRequestAsync(location.Id, dto, UserId, TenantId);

        await _sut.RejectAsync(submitted.Id, "Refuse", ReviewerId, TenantId);

        var unchangedLocation = await _dbContext.SamplingLocations.FindAsync(location.Id);
        unchangedLocation.Should().NotBeNull();
        unchangedLocation!.Name.Should().Be("Source Existante");
        unchangedLocation.LocationCode.Should().Be("SE-001");
    }

    [Fact]
    public async Task RejectAsync_WhenRequestNotFound_ShouldReturnNull()
    {
        var result = await _sut.RejectAsync(Guid.NewGuid(), "Comment", ReviewerId, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RejectAsync_WhenRequestAlreadyApproved_ShouldReturnNull()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source", "S-001", null, null, null, DistributorId);
        var submitted = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);
        await _sut.ApproveAsync(submitted.Id, null, ReviewerId, TenantId);

        var result = await _sut.RejectAsync(submitted.Id, "Try reject", ReviewerId, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RejectAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        await SeedDistributorWithUserAccess();
        var dto = new ChangeRequestCreateDto("Source", "S-001", null, null, null, DistributorId);
        var submitted = await _sut.SubmitCreateRequestAsync(dto, UserId, TenantId);

        var result = await _sut.RejectAsync(submitted.Id, "Refuse", ReviewerId, OtherTenantId);

        result.Should().BeNull();
    }

    #endregion

    #region Tenant Isolation

    [Fact]
    public async Task GetMyRequestsAsync_ShouldNotReturnRequestsFromOtherTenant()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequestsInOtherTenant();

        var result = await _sut.GetMyRequestsAsync(UserId, OtherTenantId);

        result.Should().HaveCount(1);
        result[0].DistributorName.Should().Be("Autre Distributeur");
    }

    [Fact]
    public async Task GetPendingRequestsAsync_ShouldNotReturnRequestsFromOtherTenant()
    {
        await SeedDistributorWithUserAccess();
        await SeedChangeRequests();
        await SeedChangeRequestsInOtherTenant();

        var result = await _sut.GetPendingRequestsAsync(OtherTenantId);

        result.Should().HaveCount(1);
    }

    #endregion

    #region Seed Helpers

    private async Task SeedTenantAndUsers()
    {
        if (!await _dbContext.Set<Tenant>().AnyAsync(t => t.Id == TenantId))
        {
            _dbContext.Set<Tenant>().Add(new Tenant { Id = TenantId, Name = "Canton Fribourg", Code = "FR" });
            await _dbContext.SaveChangesAsync();
        }
        if (!await _dbContext.Users.AnyAsync(u => u.Id == UserId))
        {
            _dbContext.Users.Add(new AppUser { Id = UserId, FirstName = "Jean", LastName = "Dupont", UserName = "jean", Email = "jean@test.ch", TenantId = TenantId });
            _dbContext.Users.Add(new AppUser { Id = OtherUserId, FirstName = "Marie", LastName = "Martin", UserName = "marie", Email = "marie@test.ch", TenantId = TenantId });
            _dbContext.Users.Add(new AppUser { Id = ReviewerId, FirstName = "Admin", LastName = "SAAV", UserName = "admin", Email = "admin@test.ch", TenantId = TenantId });
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task SeedDistributor()
    {
        await SeedTenantAndUsers();

        if (!await _dbContext.Distributors.AnyAsync(d => d.Id == DistributorId))
        {
            _dbContext.Distributors.Add(new Distributor
            {
                Id = DistributorId,
                Name = "Eau de Fribourg",
                IsActive = true,
                TenantId = TenantId,
            });
            await _dbContext.SaveChangesAsync();
        }
    }

    private async Task SeedDistributorWithUserAccess()
    {
        await SeedDistributor();

        if (!await _dbContext.UserDistributors.AnyAsync(ud => ud.UserId == UserId && ud.DistributorId == DistributorId))
        {
            _dbContext.UserDistributors.Add(new UserDistributor
            {
                UserId = UserId,
                DistributorId = DistributorId,
            });
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seeds a sampling location entity without user-distributor access (for unauthorized tests).
    /// </summary>
    private async Task<SamplingLocation> SeedSamplingLocationEntity()
    {
        await SeedDistributor();

        var location = new SamplingLocation
        {
            Id = Guid.NewGuid(),
            Name = "Source Existante",
            LocationCode = "SE-001",
            Latitude = 46.8,
            Longitude = 7.15,
            Description = "Description existante",
            DistributorId = DistributorId,
            IsActive = true,
        };

        _dbContext.SamplingLocations.Add(location);
        await _dbContext.SaveChangesAsync();

        return location;
    }

    /// <summary>
    /// Seeds a sampling location with user-distributor access (for standard tests).
    /// </summary>
    private async Task<SamplingLocation> SeedSamplingLocation()
    {
        await SeedDistributorWithUserAccess();

        var location = new SamplingLocation
        {
            Id = Guid.NewGuid(),
            Name = "Source Existante",
            LocationCode = "SE-001",
            Latitude = 46.8,
            Longitude = 7.15,
            Description = "Description existante",
            DistributorId = DistributorId,
            IsActive = true,
        };

        _dbContext.SamplingLocations.Add(location);
        await _dbContext.SaveChangesAsync();

        return location;
    }

    private async Task<List<SamplingLocationChangeRequest>> SeedChangeRequests()
    {
        var requests = new List<SamplingLocationChangeRequest>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RequestType = ChangeRequestType.Create,
                Status = ChangeRequestStatus.Pending,
                DistributorId = DistributorId,
                ProposedName = "Source A",
                ProposedLocationCode = "SA-001",
                RequestedById = UserId,
                RequestedAt = DateTime.UtcNow.AddHours(-2),
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                RequestType = ChangeRequestType.Update,
                Status = ChangeRequestStatus.Pending,
                DistributorId = DistributorId,
                ProposedName = "Source B",
                ProposedLocationCode = "SB-001",
                RequestedById = UserId,
                RequestedAt = DateTime.UtcNow.AddHours(-1),
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                RequestType = ChangeRequestType.Create,
                Status = ChangeRequestStatus.Approved,
                DistributorId = DistributorId,
                ProposedName = "Source C",
                ProposedLocationCode = "SC-001",
                RequestedById = OtherUserId,
                RequestedAt = DateTime.UtcNow.AddHours(-3),
                ReviewedById = ReviewerId,
                ReviewedAt = DateTime.UtcNow.AddHours(-1),
                TenantId = TenantId,
            },
        };

        _dbContext.SamplingLocationChangeRequests.AddRange(requests);
        await _dbContext.SaveChangesAsync();

        return requests;
    }

    private async Task SeedChangeRequestsInOtherTenant()
    {
        if (!await _dbContext.Set<Tenant>().AnyAsync(t => t.Id == OtherTenantId))
        {
            _dbContext.Set<Tenant>().Add(new Tenant { Id = OtherTenantId, Name = "Canton Berne", Code = "BE" });
            await _dbContext.SaveChangesAsync();
        }

        var otherDistributor = new Distributor
        {
            Id = Guid.NewGuid(),
            Name = "Autre Distributeur",
            IsActive = true,
            TenantId = OtherTenantId,
        };
        _dbContext.Distributors.Add(otherDistributor);

        _dbContext.UserDistributors.Add(new UserDistributor
        {
            UserId = UserId,
            DistributorId = otherDistributor.Id,
        });

        _dbContext.SamplingLocationChangeRequests.Add(new SamplingLocationChangeRequest
        {
            Id = Guid.NewGuid(),
            RequestType = ChangeRequestType.Create,
            Status = ChangeRequestStatus.Pending,
            DistributorId = otherDistributor.Id,
            ProposedName = "Source Autre Tenant",
            ProposedLocationCode = "SAT-001",
            RequestedById = UserId,
            RequestedAt = DateTime.UtcNow,
            TenantId = OtherTenantId,
        });

        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
