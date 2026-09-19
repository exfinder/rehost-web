using SplitSample.Verify;

await SampleVerification.Run(args.SingleOrDefault() ?? Directory.GetCurrentDirectory());
