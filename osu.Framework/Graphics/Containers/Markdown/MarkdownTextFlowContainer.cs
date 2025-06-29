﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax.Inlines;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics.Containers.Markdown.Footnotes;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Framework.Graphics.Containers.Markdown
{
    /// <summary>
    /// Markdown text flow container.
    /// </summary>
    public partial class MarkdownTextFlowContainer
        : CustomizableTextContainer,
            IMarkdownTextComponent
    {
        public float TotalTextWidth =>
            Padding.TotalHorizontal + Flow.FlowingChildren.Sum(x => x.BoundingBox.Size.X);

        [Resolved]
        private IMarkdownTextComponent parentTextComponent { get; set; }

        public MarkdownTextFlowContainer()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        protected void AddDrawable(Drawable drawable) =>
            base.AddText("[" + AddPlaceholder(drawable) + "]");

        public void AddText(string text, Action<SpriteText> creationParameters = null) =>
            base.AddText(Escape(text), creationParameters);

        public ITextPart AddParagraph(string text, Action<SpriteText> creationParameters = null) =>
            base.AddParagraph(Escape(text), creationParameters);

        public void AddInlineText(ContainerInline container)
        {
            foreach (var single in container)
            {
                switch (single)
                {
                    case LiteralInline literal:
                        string text = literal.Content.ToString();

                        if (
                            container.GetPrevious(literal) is HtmlInline
                            && container.GetNext(literal) is HtmlInline
                        )
                            AddHtmlInLineText(text, literal);
                        else if (container.GetNext(literal) is HtmlEntityInline entityInLine)
                            AddHtmlEntityInlineText(text, entityInLine);
                        else
                        {
                            switch (literal.Parent)
                            {
                                case CustomContainerInline containerInline:
                                    AddCustomComponent(containerInline);
                                    break;

                                case EmphasisInline:
                                    var parent = literal.Parent;

                                    var emphases = new List<string>();

                                    while (parent is EmphasisInline e)
                                    {
                                        emphases.Add(
                                            e.DelimiterCount == 2
                                                ? new string(e.DelimiterChar, 2)
                                                : e.DelimiterChar.ToString()
                                        );
                                        parent = parent.Parent;
                                    }

                                    addEmphasis(text, emphases);

                                    break;

                                case LinkInline linkInline:
                                {
                                    if (!linkInline.IsImage)
                                        AddLinkText(text, linkInline);
                                    break;
                                }

                                default:
                                    AddText(text);
                                    break;
                            }
                        }

                        break;

                    case CodeInline codeInline:
                        AddCodeInLine(codeInline);
                        break;

                    case LinkInline linkInline when linkInline.IsImage:
                        AddImage(linkInline);
                        break;

                    case HtmlInline:
                    case HtmlEntityInline:
                        // Handled by the next literal
                        break;

                    case LineBreakInline lineBreak:
                        if (lineBreak.IsHard)
                            NewParagraph();
                        else
                            NewLine();
                        break;

                    case ContainerInline innerContainer:
                        AddInlineText(innerContainer);
                        break;

                    case AutolinkInline autoLink:
                        AddAutoLink(autoLink);
                        break;

                    case FootnoteLink footnoteLink:
                        if (footnoteLink.IsBackLink)
                            AddFootnoteBacklink(footnoteLink);
                        else
                            AddFootnoteLink(footnoteLink);
                        break;

                    default:
                        AddNotImplementedInlineText(single);
                        break;
                }
            }
        }

        protected virtual void AddHtmlInLineText(string text, LiteralInline literalInline) =>
            AddText(text, t => t.Colour = Color4.MediumPurple);

        protected virtual void AddHtmlEntityInlineText(
            string text,
            HtmlEntityInline entityInLine
        ) => AddText(text, t => t.Colour = Color4.GreenYellow);

        protected virtual void AddLinkText(string text, LinkInline linkInline) =>
            AddDrawable(new MarkdownLinkText(text, linkInline));

        protected virtual void AddAutoLink(AutolinkInline autolinkInline) =>
            AddDrawable(new MarkdownLinkText(autolinkInline));

        protected virtual void AddCodeInLine(CodeInline codeInline) =>
            AddText(
                codeInline.Content,
                t =>
                {
                    t.Colour = Color4.Orange;
                }
            );

        protected virtual void AddImage(LinkInline linkInline) =>
            AddDrawable(new MarkdownImage(linkInline.Url));

        protected virtual void AddFootnoteLink(FootnoteLink footnoteLink) =>
            AddDrawable(new MarkdownFootnoteLink(footnoteLink));

        protected virtual void AddFootnoteBacklink(FootnoteLink footnoteBacklink) =>
            AddDrawable(new MarkdownFootnoteBacklink());

        protected virtual void AddCustomComponent(CustomContainerInline customContainerInline) =>
            AddNotImplementedInlineText(customContainerInline);

        protected virtual void AddNotImplementedInlineText(Inline inline) =>
            AddText(inline.GetType() + " not implemented.", t => t.Colour = Color4.Red);

        private void addEmphasis(string text, List<string> emphases)
        {
            bool hasItalic = false;
            bool hasBold = false;

            foreach (string e in emphases)
            {
                switch (e)
                {
                    case "*":
                    case "_":
                        hasItalic = true;
                        break;

                    case "**":
                    case "__":
                        hasBold = true;
                        break;
                }
            }

            AddText(text, t => ApplyEmphasisedCreationParameters(t, hasBold, hasItalic));
        }

        protected internal override SpriteText CreateSpriteText() =>
            parentTextComponent.CreateSpriteText();

        /// <summary>
        /// Applies emphasised creation parameters to <see cref="SpriteText"/>.
        /// </summary>
        /// <param name="spriteText">The <see cref="SpriteText"/> to be emphasised.</param>
        /// <param name="bold">Whether the text should be emboldened.</param>
        /// <param name="italic">Whether the text should be italicised.</param>
        protected virtual void ApplyEmphasisedCreationParameters(
            SpriteText spriteText,
            bool bold,
            bool italic
        ) => spriteText.Font = spriteText.Font.With(weight: bold ? "Bold" : null, italics: italic);

        SpriteText IMarkdownTextComponent.CreateSpriteText() => CreateSpriteText();
    }
}
