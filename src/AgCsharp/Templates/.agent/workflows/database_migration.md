---
description: A safe and systematic workflow for evolving database schemas without data loss or downtime using Entity Framework Core. Covers planning changes with risk assessment, creating migrations with proper Up/Down methods, generating and reviewing SQL scripts, testing migrations locally with rollback verification, deploying to staging and production environments with comprehensive checklists, and strategies for handling large tables with millions of rows. Includes best practices, do's and don'ts, and rollback plan documentation templates.
---

# Workflow: Database Migration

## Overview
Safe process for evolving database schema without data loss or downtime.

---

## Step 1: Plan the Change

### Assess Impact
- [ ] What tables/columns are affected?
- [ ] Is this a breaking change for existing data?
- [ ] Are there dependent applications?
- [ ] How much data will be affected?
- [ ] Is downtime required?

### Types of Changes

| Change Type | Risk Level | Rollback | Notes |
|-------------|------------|----------|-------|
| Add column (nullable) | Low | Easy | Safe, no data impact |
| Add column (required) | Medium | Hard | Needs default or data migration |
| Rename column | High | Hard | Breaks existing code |
| Drop column | High | Hard | Data loss, breaks code |
| Add index | Low | Easy | May take time on large tables |
| Drop index | Low | Easy | May affect performance |
| Add table | Low | Easy | No impact on existing data |
| Drop table | High | Hard | Data loss |

---

## Step 2: Create Migration

### Using EF Core CLI

```bash
# Navigate to solution root
cd /path/to/solution

# Create migration
dotnet ef migrations add AddProductRating \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api \
    --output-dir Persistence/Migrations

# List migrations
dotnet ef migrations list \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api
```

### Migration File Structure

```csharp
// Migrations/20240115120000_AddProductRating.cs
public partial class AddProductRating : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add nullable column first
        migrationBuilder.AddColumn<decimal>(
            name: "Rating",
            table: "Products",
            type: "decimal(3,2)",
            nullable: true);

        // Set default values for existing data
        migrationBuilder.Sql(
            "UPDATE Products SET Rating = 0.00 WHERE Rating IS NULL");

        // Make column required
        migrationBuilder.AlterColumn<decimal>(
            name: "Rating",
            table: "Products",
            type: "decimal(3,2)",
            nullable: false,
            defaultValue: 0m);

        // Add index for performance
        migrationBuilder.CreateIndex(
            name: "IX_Products_Rating",
            table: "Products",
            column: "Rating");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Products_Rating",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "Rating",
            table: "Products");
    }
}
```

---

## Step 3: Review the Generated SQL

### Generate SQL Script

```bash
# Generate idempotent script (safe to run multiple times)
dotnet ef migrations script \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api \
    --idempotent \
    --output migrations.sql

# Generate from specific migration
dotnet ef migrations script PreviousMigration CurrentMigration \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api \
    --output incremental.sql
```

### Review Script

```sql
-- Check for:
-- 1. Correct table/column names
-- 2. Appropriate data types
-- 3. Proper constraints
-- 4. Index creation
-- 5. Data migration logic

IF NOT EXISTS(SELECT * FROM __EFMigrationsHistory 
              WHERE MigrationId = N'20240115120000_AddProductRating')
BEGIN
    ALTER TABLE [Products] ADD [Rating] decimal(3,2) NULL;
    
    UPDATE Products SET Rating = 0.00 WHERE Rating IS NULL;
    
    DECLARE @defaultValue NVARCHAR(MAX) = N'0.00';
    EXEC('ALTER TABLE [Products] 
          ADD CONSTRAINT [DF_Products_Rating] DEFAULT ' + @defaultValue + ' FOR [Rating]');
    
    ALTER TABLE [Products] ALTER COLUMN [Rating] decimal(3,2) NOT NULL;
    
    CREATE INDEX [IX_Products_Rating] ON [Products] ([Rating]);
    
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20240115120000_AddProductRating', N'8.0.0');
END;
```

---

## Step 4: Test the Migration

### Local Testing

```bash
# Apply to local database
dotnet ef database update \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api

# Verify changes
# - Check schema in database
# - Run application
# - Test affected features
```

### Test Rollback

```bash
# Rollback to previous migration
dotnet ef database update PreviousMigrationName \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api

# Re-apply
dotnet ef database update \
    --project src/MyApp.Infrastructure \
    --startup-project src/MyApp.Api
```

### Create Test Data

```sql
-- Before migration: Create test scenarios
INSERT INTO Products (Name, Price) VALUES ('Test Product', 99.99);

-- After migration: Verify data
SELECT * FROM Products WHERE Name = 'Test Product';
-- Should show Rating = 0.00
```

---

## Step 5: Deploy to Staging

### Pre-Deployment
- [ ] Backup staging database
- [ ] Document current schema version
- [ ] Notify team of deployment window

### Deployment

```bash
# Option 1: Using EF Core
dotnet ef database update --connection "StagingConnectionString"

# Option 2: Using SQL script
sqlcmd -S staging-server -d MyAppDb -i migrations.sql

# Option 3: Using Azure DevOps/GitHub Actions
# Automated in CI/CD pipeline
```

### Post-Deployment Verification
- [ ] Schema matches expected structure
- [ ] Data integrity maintained
- [ ] Application functions correctly
- [ ] Performance acceptable

---

## Step 6: Deploy to Production

### Production Checklist

```markdown
## Pre-Deployment
- [ ] Staging deployment successful
- [ ] Full database backup completed
- [ ] Rollback plan documented
- [ ] Deployment window scheduled
- [ ] Team notified
- [ ] Monitoring dashboards ready

## Deployment
- [ ] Put application in maintenance mode (if needed)
- [ ] Apply migration
- [ ] Verify schema changes
- [ ] Resume application
- [ ] Smoke test critical paths

## Post-Deployment
- [ ] Monitor error rates
- [ ] Check performance metrics
- [ ] Verify data integrity
- [ ] Update documentation
- [ ] Remove rollback plan after stability period
```

### Rollback Plan

```sql
-- Document the rollback steps
-- Example: Revert AddProductRating

-- 1. Drop index
DROP INDEX IF EXISTS [IX_Products_Rating] ON [Products];

-- 2. Drop column
ALTER TABLE [Products] DROP CONSTRAINT IF EXISTS [DF_Products_Rating];
ALTER TABLE [Products] DROP COLUMN IF EXISTS [Rating];

-- 3. Remove migration record
DELETE FROM [__EFMigrationsHistory] 
WHERE MigrationId = N'20240115120000_AddProductRating';
```

---

## Best Practices

### Do's
- ✅ Always backup before migrating production
- ✅ Test migrations on a copy of production data
- ✅ Use idempotent scripts for production
- ✅ Deploy during low-traffic periods
- ✅ Monitor closely after deployment
- ✅ Keep migrations small and focused

### Don'ts
- ❌ Don't drop columns without proper deprecation
- ❌ Don't rename columns directly (add new, migrate, drop old)
- ❌ Don't add required columns without defaults
- ❌ Don't skip staging testing
- ❌ Don't deploy without rollback plan
- ❌ Don't combine many changes in one migration

---

## Large Table Strategies

### For tables with millions of rows:

```sql
-- Add column in batches to avoid locks
-- 1. Add nullable column
ALTER TABLE Products ADD Rating decimal(3,2) NULL;

-- 2. Update in batches
DECLARE @BatchSize INT = 10000;
DECLARE @Rows INT = 1;

WHILE @Rows > 0
BEGIN
    UPDATE TOP (@BatchSize) Products
    SET Rating = 0.00
    WHERE Rating IS NULL;
    
    SET @Rows = @@ROWCOUNT;
    
    WAITFOR DELAY '00:00:01'; -- Brief pause between batches
END

-- 3. Make non-nullable
ALTER TABLE Products ALTER COLUMN Rating decimal(3,2) NOT NULL;
```
