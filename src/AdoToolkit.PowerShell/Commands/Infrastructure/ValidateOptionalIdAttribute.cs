namespace AdoToolkit;

// PowerShell's sealed ValidateRangeAttribute rejects null even with AllowNull.
// Missing log IDs must reach ProcessRecord to produce BuildLogNotAvailable per input, and
// Set-AdoProfile removes a default ID with $null.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
internal sealed class ValidateOptionalIdAttribute(AdoMessage message) : ValidateArgumentsAttribute
{
    public ValidateOptionalIdAttribute() : this(AdoMessage.BuildLogIdRange) { }

    public AdoMessage Message { get; } = message;

    protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
    {
        if (arguments is null || arguments is int value && value >= 1) return;
        throw new ValidationMetadataException(Messages.Get(Message, CultureCapture.Capture(engineIntrinsics.SessionState)));
    }
}
