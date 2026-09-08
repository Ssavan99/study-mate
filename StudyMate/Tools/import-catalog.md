# Importing a course catalog

Catalogs are checked in at `Data/Catalog/<university-slug>.json`. They are loaded at
startup and are idempotent: an existing `(UniversityId, Code)` course is reused.

```json
{
  "departments": [
    {
      "code": "CSCE",
      "courses": [
        { "code": "CSCE 310", "title": "Data Structures and Algorithms" }
      ]
    }
  ]
}
```

To add a university, add its name, stable slug, and recognised email domains to
`DataSeeder.SeedUniversitiesAndCatalogAsync`, then add the matching JSON file. A future
university feed should transform into this small JSON shape before it is checked in; the
web app deliberately does not depend on a live catalog service.
