using FluentValidation;
using Microsoft.Extensions.Logging;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Common;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Application.Mapping;
using ResellerSystem.Server.Domain.Entities;
using ResellerSystem.Server.Domain.Enums;

namespace ResellerSystem.Server.Application.Databases;

/// <summary>
/// Orchestrates tenant database creation end-to-end:
///
///   1. Reserve an immutable physical name ("reseller_db_000015") derived
///      from an internal sequence — NEVER from the user-supplied display name.
///   2. Register the tenant in the master database immediately, with
///      Status = Creating, so a crash mid-provisioning is visible rather
///      than silently missing.
///   3. Create the physical PostgreSQL database.
///   4. Apply all tenant migration scripts.
///   5. Mark the tenant Ready (success) or MigrationFailed (failure) —
///      never leave it looking like it succeeded when it didn't.
///
/// Rename only ever touches the display Name; PhysicalDatabaseName is
/// immutable for the lifetime of the tenant.
/// </summary>
public sealed class DatabaseProvisioningService : IDatabaseProvisioningService
{
    private const string PhysicalNamePrefix = "reseller_db_";

    private readonly IDatabaseProfileRepository _repository;
    private readonly ITenantDatabaseProvisioner _provisioner;
    private readonly ITimeZoneValidator _timeZoneValidator;
    private readonly IValidator<CreateDatabaseRequest> _createValidator;
    private readonly IValidator<UpdateDatabaseRequest> _updateValidator;
    private readonly ILogger<DatabaseProvisioningService> _logger;

    public DatabaseProvisioningService(
        IDatabaseProfileRepository repository,
        ITenantDatabaseProvisioner provisioner,
        ITimeZoneValidator timeZoneValidator,
        IValidator<CreateDatabaseRequest> createValidator,
        IValidator<UpdateDatabaseRequest> updateValidator,
        ILogger<DatabaseProvisioningService> logger)
    {
        _repository = repository;
        _provisioner = provisioner;
        _timeZoneValidator = timeZoneValidator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<DatabaseProfileDto> CreateAsync(CreateDatabaseRequest request, CancellationToken ct = default)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationFailedException(validation.Errors.Select(e => e.ErrorMessage).ToList());

        _timeZoneValidator.TryNormalize(request.TimeZone, out var normalizedTimeZone);

        var sequence = await _repository.GetNextPhysicalSequenceAsync(ct);
        var physicalName = $"{PhysicalNamePrefix}{sequence:D6}";

        if (await _repository.PhysicalNameExistsAsync(physicalName, ct))
        {
            // Should be unreachable given a monotonic sequence, but never trust
            // a generated identifier blindly for something as sensitive as a DB name.
            throw new ConflictException("PHYSICAL_NAME_COLLISION",
                $"Generated physical database name '{physicalName}' already exists.");
        }

        var profile = DatabaseProfile.CreateNew(request.Name, physicalName, normalizedTimeZone, request.Currency);
        await _repository.AddAsync(profile, ct);

        _logger.LogInformation(
            "Provisioning tenant database {DatabaseId} ({PhysicalName}) for display name {DisplayName}",
            profile.Id, physicalName, profile.Name);

        try
        {
            if (!await _provisioner.DatabaseExistsAsync(physicalName, ct))
            {
                await _provisioner.CreateDatabaseAsync(physicalName, ct);
            }

            var schemaVersion = await _provisioner.ApplyTenantMigrationsAsync(physicalName, ct);

            profile.MarkReady(schemaVersion);
            await _repository.UpdateAsync(profile, ct);

            _logger.LogInformation(
                "Tenant database {DatabaseId} ({PhysicalName}) is Ready at schema version {SchemaVersion}",
                profile.Id, physicalName, schemaVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Provisioning failed for tenant database {DatabaseId} ({PhysicalName}); marking MigrationFailed",
                profile.Id, physicalName);

            profile.MarkMigrationFailed();
            await _repository.UpdateAsync(profile, ct);

            throw new DatabaseNotReadyException(
                $"Database '{profile.Name}' could not be provisioned. It has been recorded with status MigrationFailed.");
        }

        return profile.ToDto();
    }

    public async Task<DatabaseProfileDto> UpdateAsync(Guid id, UpdateDatabaseRequest request, CancellationToken ct = default)
    {
        var validation = await _updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationFailedException(validation.Errors.Select(e => e.ErrorMessage).ToList());

        var profile = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("DATABASE_NOT_FOUND", "Database was not found.");

        // Rename only ever touches the display name — PhysicalDatabaseName is untouched.
        if (request.Name is not null)
        {
            profile.Rename(request.Name);
        }

        if (request.TimeZone is not null)
        {
            _timeZoneValidator.TryNormalize(request.TimeZone, out var normalized);
            profile.ChangeTimeZone(normalized);
        }

        if (request.IsActive is not null)
        {
            profile.SetActive(request.IsActive.Value);
        }

        await _repository.UpdateAsync(profile, ct);
        return profile.ToDto();
    }

    public async Task<DatabaseProfileDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("DATABASE_NOT_FOUND", "Database was not found.");
        return profile.ToDto();
    }

    public async Task<IReadOnlyList<DatabaseProfileDto>> ListAsync(CancellationToken ct = default)
    {
        var profiles = await _repository.GetAllAsync(ct);
        return profiles.Select(p => p.ToDto()).ToList();
    }
}
