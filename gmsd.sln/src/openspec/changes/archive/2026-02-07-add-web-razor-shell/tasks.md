## 1. Infrastructure Setup
- [x] 1.1 Verify `Gmsd.Web.csproj` has Razor Pages SDK and service references
- [x] 1.2 Configure Razor Pages services in `Program.cs` (AddRazorPages, routing)
- [x] 1.3 Register `IProjectService` dependency injection in `Program.cs`

## 2. Layout and Shared Components
- [x] 2.1 Create `Pages/_ViewImports.cshtml` for namespace imports
- [x] 2.2 Create `Pages/_ViewStart.cshtml` with Layout assignment
- [x] 2.3 Create `Pages/Shared/_Layout.cshtml` with navigation header and footer
- [x] 2.4 Add `_ValidationScriptsPartial.cshtml` for validation scripts

## 3. Static Assets
- [x] 3.1 Create `wwwroot/css/site.css` with basic styling (layout, typography, tables)
- [x] 3.2 Create `wwwroot/js/site.js` with minimal interactivity
- [x] 3.3 Ensure static file middleware is configured in `Program.cs`

## 4. Home Page
- [x] 4.1 Create `Pages/Index.cshtml` and `Index.cshtml.cs` page model
- [x] 4.2 Add dashboard-style landing page with links to Projects

## 5. Project List Page (Read-Only)
- [x] 5.1 Create `Pages/Projects/Index.cshtml` and `Index.cshtml.cs`
- [x] 5.2 Inject `IProjectService` into page model
- [x] 5.3 Display projects in table format with Name, Description, CreatedAt
- [x] 5.4 Add link to detail view for each project
- [x] 5.5 Add empty state when no projects exist

## 6. Project Detail Page (Read-Only)
- [x] 6.1 Create `Pages/Projects/Details.cshtml` and `Details.cshtml.cs`
- [x] 6.2 Accept `id` route parameter (`{id}`)
- [x] 6.3 Display full project details (Name, Description, CreatedAt, UpdatedAt)
- [x] 6.4 Handle NotFoundException with 404 or friendly error page
- [x] 6.5 Add navigation back to list view

## 7. Verification
- [x] 7.1 Launch web app and verify homepage renders
- [x] 7.2 Verify project list page displays data via `IProjectService`
- [x] 7.3 Verify project detail page loads with correct ID
- [x] 7.4 Verify 404 handling for non-existent project IDs
