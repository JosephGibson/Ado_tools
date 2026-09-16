namespace AdoToolkit;

// PowerShell's sealed ValidateRangeAttribute rejects null even with AllowNull.
// Missing log IDs must reach ProcessRecord to produce BuildLogNotAvailable per input.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
internal sealed class ValidateOptionalIdAttribute : ValidateArgumentsAttribute
{
    protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
    {
        if (arguments is null || arguments is int value && value >= 1) return;
        throw new ValidationMetadataException(Messages.Get(AdoMessage.BuildLogIdRange,
            CultureCapture.Capture(engineIntrinsics.SessionState)));
    }
}
