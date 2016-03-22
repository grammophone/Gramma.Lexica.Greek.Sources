using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Gramma.LanguageModel.Greek.TrainingSources.Perseus;
using Gramma.LanguageModel.TrainingSources;
using Gramma.Lexica.LexiconModel;

namespace Gramma.Lexica.Greek.Sources.Perseus
{
	/// <summary>
	/// A lexicon source from Perseus XML files.
	/// </summary>
	public class PerseusLexiconSource : TrainingSource<Lemma>
	{
		#region Auxilliary types

		private class Line
		{
			public StringBuilder TextBuilder = new StringBuilder();

			public string Reference;
		}

		private class RawSense
		{
			public string Description;

			public string Reference;

			public string Label;

			public int Level;

			public List<RawSense> Subsenses = new List<RawSense>();
		}

		#endregion

		#region Private fields

		private BetaImport.BetaConverter betaConverter;

		private string sourceFilename;

		private XmlReader reader;

		private Stream stream;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		public PerseusLexiconSource()
		{
			betaConverter = new BetaImport.PrecombinedDiacriticsBetaConverter();
		}

		#endregion

		#region Public properties

		/// <summary>
		/// The name of the Perseus XML file of the lexicon.
		/// </summary>
		public string SourceFilename
		{
			get
			{
				return sourceFilename;
			}
			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));
				sourceFilename = value;
			}
		}

		#endregion

		#region Protected methods

		protected override void CloseImplementation()
		{
			if (reader != null)
			{
				reader.Close();

				((IDisposable)reader).Dispose();

				reader = null;
			}

			if (stream != null)
			{
				stream.Close();

				stream.Dispose();

				stream = null;
			}
		}

		protected override IEnumerable<Lemma> GetDataImplementation()
		{
			var languageProvider = this.LanguageProvider;

			while (reader.Read())
			{
				if (!reader.ReadToFollowing("entry")) yield break;

				string betaKey = reader.GetAttribute("key");

				if (betaKey == null) continue;

				string key = betaConverter.Convert(betaKey.NormalizeBeta());

				string form = key.StripNumerics();

				Etymology etymology = null;

				var rawSenses = new List<RawSense>();

				var notes = new List<string>();

				Line line;

				while (reader.Read())
				{
					switch (reader.NodeType)
					{
						case XmlNodeType.Element:
							switch (reader.Name)
							{
								case "etym":
									line = ReadLine();
									etymology = new Etymology(line.TextBuilder.ToString(), line.Reference);
									break;

								case "sense":
									int level = 0;
									string levelString = reader.GetAttribute("level");
									if (levelString != null) Int32.TryParse(levelString, out level);

									string label = reader.GetAttribute("n");

									if (label == "0") label = null;

									line = ReadLine();

									var rawSense = new RawSense
									{
										Label = label, Description = line.TextBuilder.ToString(), Level = level, Reference = line.Reference
									};

									rawSenses.Add(rawSense);

									break;

								case "note":
									line = ReadLine();
									notes.Add(line.TextBuilder.ToString());
									break;
							}

							break;
					}

					if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "entry") break;
				}

				var senses = ProcessSenses(rawSenses);

				yield return new Lemma(key, form, senses, notes, etymology);

			}
		}

		protected override void OpenImplementation()
		{
			stream = new FileStream(this.SourceFilename, FileMode.Open, FileAccess.Read);

			reader = new XmlTextReader(stream);
		}

		#endregion

		#region Private methods

		private Line ReadLine()
		{
			var line = new Line();

			ReadLine(line);

			return line;
		}

		private void ReadLine(Line line)
		{
			while (!reader.EOF)
			{
				switch (reader.NodeType)
				{
					case XmlNodeType.Element:
						bool isInnerBetaCode = reader.GetAttribute("lang")?.Equals("greek") ?? false;
						bool isInnerReference = reader.Name == "ref";

						reader.Read();

						ReadLine(line, isInnerBetaCode, isInnerReference);

						return;
				}
			}
		}

		private void ReadLine(Line line, bool isBetaCode, bool isReference)
		{
			while (!reader.EOF)
			{
				switch (reader.NodeType)
				{
					case XmlNodeType.Element:
						bool isInnerBetaCode = reader.GetAttribute("lang")?.Equals("greek") ?? false;
						bool isInnerReference = reader.Name == "ref";

						reader.Read();

						ReadLine(line, isInnerBetaCode, isInnerReference);

						break;

					case XmlNodeType.Text:
						string text;

						if (isBetaCode)
						{
							string betaText = reader.Value.NormalizeBeta();

							text = betaConverter.Convert(betaText);
						}
						else
						{
							text = reader.Value;
						}

						if (isReference)
						{
							line.Reference = text;

							text = text.StripNumerics();
						}

						if (text.Length > 0)
						{
							if (line.TextBuilder.Length > 0 && line.TextBuilder[line.TextBuilder.Length - 1] != ' ' && text[0] != ' ')
								line.TextBuilder.Append(' ');

							line.TextBuilder.Append(text);
						}

						reader.Read();

						break;

					case XmlNodeType.EndElement:
						reader.Read();
						return;

					default:
						reader.Read();
						break;
				}
			}

		}

		private IReadOnlyList<Sense> ProcessSenses(IReadOnlyList<RawSense> rawSenses)
		{
			if (rawSenses.Count > 0)
			{
				int minimumLevel = rawSenses[0].Level;

				foreach (var rawSense in rawSenses)
				{
					rawSense.Level = Math.Max(rawSense.Level - minimumLevel, 0);
				}
			}

			var rootRawSenses = new List<RawSense>();

			int index = 0;

			ProcessRawSensesLevel(rawSenses, 0, ref index, rootRawSenses);

			return TransformSenses(rootRawSenses);
		}

		private void ProcessRawSensesLevel(IReadOnlyList<RawSense> rawSenses, int level, ref int index, List<RawSense> levelRawSenses)
		{
			for (; index < rawSenses.Count; index++)
			{
				var rawSense = rawSenses[index];

				if (rawSense.Level == level)
				{
					levelRawSenses.Add(rawSense);
				}
				else if (rawSense.Level > level && index > 0)
				{
					ProcessRawSensesLevel(rawSenses, rawSense.Level, ref index, rawSenses[index - 1].Subsenses);
				}
				else if (rawSense.Level < level)
				{
					index--;
					return;
				}
			}
		}

		private List<Sense> TransformSenses(List<RawSense> rawSenses)
		{
			var senses = new List<Sense>(rawSenses.Count);

			foreach (var rawSense in rawSenses)
			{
				var subsenses = TransformSenses(rawSense.Subsenses);

				var sense = new Sense(rawSense.Description, rawSense.Label, subsenses);

				senses.Add(sense);
			}

			return senses;
		}

		#endregion
	}
}
