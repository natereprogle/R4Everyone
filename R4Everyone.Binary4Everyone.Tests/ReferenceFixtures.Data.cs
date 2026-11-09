namespace R4Everyone.Binary4Everyone.Tests;

/// <summary>
/// Files written by the r4cce reference implementation (r4cce_formats.py) for the database in
/// <see cref="ReferenceFixtures.BuildDatabase"/>, so these tests check compatibility with another implementation
/// rather than only round-tripping our own output.
/// </summary>
internal static partial class ReferenceFixtures
{
    public static readonly byte[] CycloEvo = Convert.FromBase64String(
        "RXZvbHV0aW9uQ2hlYXRzAAEAAAAAAAAAAAAAAAAAAACGlO4ypPuCgEUEnUM6vnvVPFD/uC5xrwyoAwyNMzCEARRTso8xIhetuaJ5" +
        "dIJoJS95u08zHg30Fu7PlDW757pkdT8ZWKQw6LrOZL0mqrPVmivRuH0W+4KAkpDvUN4F/6ydFkoVYQBkB3lVpzg+Yp8kyC7fstsQ" +
        "f/87bFA9ZvpABnuFkREitN9RyoH6pheIsydRcpsofoEv6kqQNIo1tqa/ueNPcTh2SJqpExcGblNlqbukDIJb4C2Q3JFNNBIrw1LX" +
        "nmN0wrVaeT44mhZ6JS9RQ5wmc/2ahb6NeOh3W7yV0O9LCalAMkraceW3i1rw8JcEhadkYcCSnebFz2cItP48UgNjIo6K8L4uPBfN" +
        "s4FrkwFbYELEfZA13m3BnRHaxjfOOjyIdTo/ByP4c6ZbnkmFuxu/N2hh6GoY0bh9DvuCgH0fcqLgXPIdW0KZxS8Kp+dSVqc4OmKf" +
        "JMgu37LbEP/+O2yw2UWE+QQWkVKShnVhm5jZUYuGQ80LNllfstms1oym59kkO8qA3f8lSfJOg44cydQxZcG1oPYr8kxS64fy1EO2" +
        "es8SYP59o4ORsXuSa9NVSimYkjHrMdgq5CwLx9SJpw+emT8KAgLygSs9aM3IFPQR1gKG8drxwVPRV67WYF0Y75AkiOy0hHSbZvVa" +
        "7KbZq5QfU4sMkdvq1N6gm/G4Tlj39P3meTJbnsmFuxu/Nw==");

    public static readonly byte[] Edge = Convert.FromBase64String(
        "RURHRQAAAACKRHi81QZDw/2KFA7eNS+kaaGkcQdM2BetFr8GZrF/Fzz9+Na93UvYxh/fC7ya/X/+svhO6TvKCMxxMsffZZQG5Ql7" +
        "zCwVDLtlDCy8ZgZDwyoeZh06jqvdyOcR3GDI3plWfaW4zivtNJ5/kPd9bM/TycDkYyE0dGvTtyxQJ14I+54RbEGY5M90RSjj4JUV" +
        "DueRuk3TGB9ICZgK+caXIre1lQKN9ppCFMjkYxMCl4G3GbbELuYyjakpgrTOReTU42EB6pCjJNy9WmhOBNfDtT4KhctL09H2UxRL" +
        "d9UBhz+L0UQQ2lfWTxJzq+uMj93RCCDzwQEx6uGhULdrlYcHLJ+H/d4mzFXulNmvbORfoi/tXV+Wv0tHi4lyaEaMpIeWi1lkNkfL" +
        "86z0xL7xaJ088X6NVgwsvH4GQ8PFkfvvBNembA6zwgwuwh15vX2luMor7TSef5D3fWxP0snABIcCSs1pvqPv04OftjHMScdsCS+x" +
        "WCIDJ3oyOPeBfc2gfRZjbmvezP9F4ddxM/XFq5U1pNGXdDVT0ifmaF0p4xkdMsaUBvapPIBd89LLapgjLczIeTrz+MaINehvPa+8" +
        "PT21QMTGVr33la/ACR/B5LveCk44FW49+rwSx/rgDetecmq56+iR85BQ00rJ4Hh2LlJtY26Nu5ubBudBlLz0Q6q6KE1f86x0xL7x" +
        "aJ0=");

    public static readonly byte[] M3RealExtendedSjis = Convert.FromBase64String(
        "AgBBU01FREhUMkYwMEI1NDkyAAAACAEAAEFEQUVESFQxMjM0NTY3ODoBAADPAAAADQo7R2FtZSBJRDogQVNNRSAyRjAwQjU0OQ0K" +
        "O1N1cGVyIE1hcmlvIDY0IERTDQoNCg0KPGxvb3NlIG5vdGU6Pg0KWzk5IExpdmVzXQ0KMDIwOUY0OUMgMDAwMDAwNjMNCg0KPC0t" +
        "LS0tLS0tLS06Pg0KPFBpY2sgb25lOj4NCjxNb3ZlbWVudDo+ID0gMg0KPEhvbGQgTDo+DQpbTW9vbiBKdW1wXQ0KOTQwMDAxMzAg" +
        "RkRGRjAwMDANCjAwMDAwMDAxIDAwMDAwMDAyDQoNCltTcGVlZF0NCjEyMzQ1Njc4IDlBQkNERUYwDQoNCjwtLS0tLS0tLS0tOj4N" +
        "Cg0KW1RhaWxdDQoNCgA7R2FtZSBJRDogQURBRSAxMjM0NTY3OA0KO4N8g1CDgoOTIINfg0ODhIOCg5ODaA0KDQoNCjxFbXB0eTo+" +
        "ID0gMA0KPC0tLS0tLS0tLS06Pg0KDQo8g4GDgiBdXVtbgtwgIDo+DQpblryRTz48PCAggrxdDQpERUFEQkVFRiAwMDAwMDAwMA0K" +
        "DQo8LS0tLS0tLS0tLTo+DQo8SXRlbXNbeF06PiA9IDENCltNYXN0ZXIgQmFsbF0NCjAwMDAwMDAxIDAwMDAwMDAyDQoNCgA=");

    public static readonly byte[] M3RealDhtSjis = Convert.FromBase64String(
        "AgBBU01FREhUMkYwMEI1NDkyAAAAqQAAAEFEQUVESFQxMjM0NTY3ONsAAACBAAAADQo7R2FtZSBJRDogQVNNRSAyRjAwQjU0OQ0K" +
        "Wzk5IExpdmVzXQ0KMDIwOUY0OUMgMDAwMDAwNjMNCg0Ke01vdmVtZW50On0gPSAyDQpbTW9vbiBKdW1wXQ0KOTQwMDAxMzAgRkRG" +
        "RjAwMDANCjAwMDAwMDAxIDAwMDAwMDAyDQoNCltTcGVlZF0NCjEyMzQ1Njc4IDlBQkNERUYwDQoNCltUYWlsXQ0KDQoAO0dhbWUg" +
        "SUQ6IEFEQUUgMTIzNDU2NzgNCjxFbXB0eTo+ID0gMA0KW5a8kU8+PDwgIIK8XQ0KREVBREJFRUYgMDAwMDAwMDANCg0KPEl0ZW1z" +
        "W3hdOj4gPSAxDQpbTWFzdGVyIEJhbGxdDQowMDAwMDAwMSAwMDAwMDAwMg0KDQoA");

    public static readonly byte[] M3RealNoteAfterSjis = Convert.FromBase64String(
        "AgBBU01FREhUMkYwMEI1NDkyAAAACAEAAEFEQUVESFQxMjM0NTY3ODoBAADPAAAADQo7R2FtZSBJRDogQVNNRSAyRjAwQjU0OQ0K" +
        "O1N1cGVyIE1hcmlvIDY0IERTDQoNCg0KWzk5IExpdmVzXQ0KMDIwOUY0OUMgMDAwMDAwNjMNCjxsb29zZSBub3RlOj4NCg0KPC0t" +
        "LS0tLS0tLS06Pg0KPE1vdmVtZW50Oj4gPSAyDQo8UGljayBvbmU6Pg0KW01vb24gSnVtcF0NCjk0MDAwMTMwIEZERkYwMDAwDQow" +
        "MDAwMDAwMSAwMDAwMDAwMg0KPEhvbGQgTDo+DQoNCltTcGVlZF0NCjEyMzQ1Njc4IDlBQkNERUYwDQoNCjwtLS0tLS0tLS0tOj4N" +
        "Cg0KW1RhaWxdDQoNCgA7R2FtZSBJRDogQURBRSAxMjM0NTY3OA0KO4N8g1CDgoOTIINfg0ODhIOCg5ODaA0KDQoNCjxFbXB0eTo+" +
        "ID0gMA0KPC0tLS0tLS0tLS06Pg0KDQpblryRTz48PCAggrxdDQpERUFEQkVFRiAwMDAwMDAwMA0KPIOBg4IgXV1bW4LcICA6Pg0K" +
        "DQo8LS0tLS0tLS0tLTo+DQo8SXRlbXNbeF06PiA9IDENCltNYXN0ZXIgQmFsbF0NCjAwMDAwMDAxIDAwMDAwMDAyDQoNCgA=");

    public static readonly byte[] M3RealExtendedUtf8NoFix = Convert.FromBase64String(
        "AgBBU01FREhUMkYwMEI1NDkyAAAACAEAAEFEQUVESFQxMjM0NTY3ODoBAADgAAAADQo7R2FtZSBJRDogQVNNRSAyRjAwQjU0OQ0K" +
        "O1N1cGVyIE1hcmlvIDY0IERTDQoNCg0KPGxvb3NlIG5vdGU6Pg0KWzk5IExpdmVzXQ0KMDIwOUY0OUMgMDAwMDAwNjMNCg0KPC0t" +
        "LS0tLS0tLS06Pg0KPFBpY2sgb25lOj4NCjxNb3ZlbWVudDo+ID0gMg0KPEhvbGQgTDo+DQpbTW9vbiBKdW1wXQ0KOTQwMDAxMzAg" +
        "RkRGRjAwMDANCjAwMDAwMDAxIDAwMDAwMDAyDQoNCltTcGVlZF0NCjEyMzQ1Njc4IDlBQkNERUYwDQoNCjwtLS0tLS0tLS0tOj4N" +
        "Cg0KW1RhaWxdDQoNCgA7R2FtZSBJRDogQURBRSAxMjM0NTY3OA0KO+ODneOCseODouODsyDjg4DjgqTjg6Tjg6Ljg7Pjg4kNCg0K" +
        "DQo8RW1wdHk6PiA9IDANCjwtLS0tLS0tLS0tOj4NCg0KPOODoeODoiA+fXs844OewrE6Pg0KW+WQjeWJjV08W+eKoOOCvl0NCkRF" +
        "QURCRUVGIDAwMDAwMDAwDQoNCjwtLS0tLS0tLS0tOj4NCjxJdGVtc3t4fTo+ID0gMQ0KW01hc3RlciBCYWxsXQ0KMDAwMDAwMDEg" +
        "MDAwMDAwMDINCg0KAA==");
}
