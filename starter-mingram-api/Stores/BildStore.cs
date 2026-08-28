class BildStore
{
    internal List<Bild> Bilder { get; } =
    [
        new(1, "demo.jpg", "Demobild — ersätt med din egen", ["demo", "placeholder"],
            "https://placehold.co/400x300?text=MinGram")
    ];

    internal int NastaBildId { get; set; } = 2;
}
