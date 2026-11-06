using System.Collections.Generic;
using System.Linq;
using Ink.Parsed;

namespace Ink
{
    public partial class InkParser
    {
        protected Conditional InnerConditionalContent()
        {
            var initialQueryExpression = Parse(ConditionExpression);
            var conditional = Parse(() => InnerConditionalContent (initialQueryExpression));
            if (conditional == null)
                return null;

            return conditional;
        }

        protected Conditional InnerConditionalContent(Expression initialQueryExpression)
        {
            List<ConditionalSingleBranch> alternatives;

            bool canBeInline = initialQueryExpression != null;
            bool isInline = Parse(Newline) == null;

            if (isInline && !canBeInline) {
                return null;
            }

            // Inline innards
            if (isInline) {
                alternatives = InlineConditionalBranches ();
            } 

            // Multiline innards
            else {
                alternatives = MultilineConditionalBranches ();
                if (alternatives == null) {

                    // Allow single piece of content within multi-line expression, e.g.:
                    // { true: 
                    //    Some content that isn't preceded by '-'
                    // }
                    if (initialQueryExpression) {
                        List<Parsed.Object> soleContent = StatementsAtLevel (StatementLevel.InnerBlock);
                        if (soleContent != null) {
                            var soleBranch = new ConditionalSingleBranch (soleContent);
                            alternatives = new List<ConditionalSingleBranch> ();
                            alternatives.Add (soleBranch);

                            // Also allow a final "- else:" clause
                            var elseBranch = Parse (SingleMultilineCondition);
                            if (elseBranch) {
                                if (!elseBranch.isElse) {
                                    ErrorWithParsedObject ("Expected an '- else:' clause here rather than an extra condition", elseBranch);
                                    elseBranch.isElse = true;
                                }
                                alternatives.Add (elseBranch);
                            }
                        }
                    }

                    // Still null?
                    if (alternatives == null) {
                        return null;
                    }
                } 

                // Empty true branch - didn't get parsed, but should insert one for semantic correctness,
                // and to make sure that any evaluation stack values get tidied up correctly.
                else if (alternatives.Count == 1 && alternatives [0].isElse && initialQueryExpression) {
                    var emptyTrueBranch = new ConditionalSingleBranch (null);
                    emptyTrueBranch.isTrueBranch = true;
                    alternatives.Insert (0, emptyTrueBranch);
                }

                // An explicit else is only valid once, at the end, in every multiline form.
                for (int i = 0; i < alternatives.Count - 1; ++i) {
                    if (alternatives [i].isElse) {
                        var finalClause = alternatives [alternatives.Count - 1];
                        if (finalClause.isElse) {
                            ErrorWithParsedObject ("Multiple 'else' cases. Can have a maximum of one, at the end.", finalClause);
                        } else {
                            ErrorWithParsedObject ("'else' case in conditional should always be the final one", alternatives [i]);
                        }
                    }
                }

                // Like a switch statement
                // { initialQueryExpression:
                //    ... match the expression
                // }
                if (initialQueryExpression) {

                    bool branchesHaveOwnExpression = alternatives.Any (branch => branch.ownExpression != null);
                    for (int i = 0; i < alternatives.Count; ++i) {
                        var branch = alternatives [i];

                        // Switch cases match the initial query by equality. The else
                        // also needs this flag to tidy up the evaluation stack.
                        if (branch.ownExpression || branch.isElse) {
                            branch.matchingEquality = branchesHaveOwnExpression;
                        }

                        // A bare first branch is the body of a boolean condition.
                        // If any branch has a case expression, all cases must have one.
                        else if (!branchesHaveOwnExpression && i == 0) {
                            branch.isTrueBranch = true;
                        } else {
                            ErrorWithParsedObject ("Branch doesn't have condition. Expected an expression followed by ':', or '- else:' for a fallback.", branch);
                        }
                    }
                } 

                // No initial query, so just a multi-line conditional. e.g.:
                // {
                //   - x > 3:  greater than three
                //   - x == 3: equal to three
                //   - x < 3:  less than three
                // }
                else {
                    foreach (var alt in alternatives) {
                        if (alt.ownExpression == null && !alt.isElse) {
                            ErrorWithParsedObject ("Branch doesn't have condition. Expected an expression followed by ':', or '- else:' for a fallback.", alt);
                        }
                    }
                        
                    if (alternatives.Count == 1 && alternatives [0].ownExpression == null) {
                        ErrorWithParsedObject ("Condition block with no conditions", alternatives [0]);
                    }
                }
            }

            if (alternatives == null)
                return null;

            foreach (var branch in alternatives) {
                branch.isInline = isInline;
            }

            var cond = new Conditional (initialQueryExpression, alternatives);
            return cond;
        }

        protected List<ConditionalSingleBranch> InlineConditionalBranches()
        {
            var listOfLists = Interleave<List<Parsed.Object>> (MixedTextAndLogic, Exclude (String ("|")), flatten: false);
            if (listOfLists == null || listOfLists.Count == 0) {
                return null;
            }

            var result = new List<ConditionalSingleBranch> ();

            if (listOfLists.Count > 2) {
                Error ("Expected one or two alternatives separated by '|' in inline conditional");
            } else {
                
                var trueBranch = new ConditionalSingleBranch (listOfLists[0]);
                trueBranch.isTrueBranch = true;
                result.Add (trueBranch);

                if (listOfLists.Count > 1) {
                    var elseBranch = new ConditionalSingleBranch (listOfLists[1]);
                    elseBranch.isElse = true;
                    result.Add (elseBranch);
                }
            }

            return result;
        }

        protected List<ConditionalSingleBranch> MultilineConditionalBranches()
        {
            MultilineWhitespace ();

            List<object> multipleConditions = OneOrMore (SingleMultilineCondition);
            if (multipleConditions == null)
                return null;
            
            MultilineWhitespace ();

            return multipleConditions.Cast<ConditionalSingleBranch>().ToList();
        }

        protected ConditionalSingleBranch SingleMultilineCondition()
        {
            Whitespace ();

            // Make sure we're not accidentally parsing a divert
            if (ParseString ("->") != null)
                return null;

            if (ParseString ("-") == null)
                return null;

            Whitespace ();

            Expression expr = null;
            bool isElse = Parse(ElseExpression) != null;

            if( !isElse )
                expr = Parse(ConditionExpression);

            List<Parsed.Object> content = StatementsAtLevel (StatementLevel.InnerBlock);
            if (expr == null && content == null) {
                Error ("expected content for the conditional branch following '-'");

                // Recover
                content = new List<Ink.Parsed.Object> ();
                content.Add (new Text (""));
            }

            // Allow additional multiline whitespace, if the statements were empty (valid)
            // then their surrounding multiline whitespacce needs to be handled manually.
            // e.g.
            // { x:
            //   - 1:    // intentionally left blank, but newline needs to be parsed
            //   - 2: etc
            // }
            MultilineWhitespace ();

            var branch = new ConditionalSingleBranch (content);
            branch.ownExpression = expr;
            branch.isElse = isElse;
            return branch;
        }

        protected Expression ConditionExpression()
        {
            var expr = Parse(Expression);
            if (expr == null)
                return null;

            DisallowIncrement (expr);

            Whitespace ();

            if (ParseString (":") == null)
                return null;

            return expr;
        }

        protected object ElseExpression()
        {
            if (ParseString ("else") == null)
                return null;

            Whitespace ();

            if (ParseString (":") == null)
                return null;

            return ParseSuccess;
        }
    }
}

