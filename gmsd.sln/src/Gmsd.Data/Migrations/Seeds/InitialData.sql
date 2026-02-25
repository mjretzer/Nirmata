-- Initial Seed Data for Projects Table
-- This SQL file serves as reference documentation for the baseline seed data
-- that is applied automatically via EF Core migrations.

-- Projects
INSERT INTO Project (ProjectId, Name) VALUES
    ('proj-sample-001', 'Sample Web Application'),
    ('proj-sample-002', 'API Migration Project'),
    ('proj-sample-003', 'Database Optimization Initiative');

-- Note: Steps are not seeded initially as they are child entities
-- that can be added to projects through the application UI or API.
