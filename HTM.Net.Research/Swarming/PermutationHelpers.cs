﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using HTM.Net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HTM.Net.Research.Swarming
{
    /// <summary>
    /// "The base class of all PermuteXXX classes that can be used from within a permutation script.
    /// </summary>
    [Serializable]
    public abstract class PermuteVariable
    {
        protected PermuteVariable()
        {
            Type = GetType().AssemblyQualifiedName;
        }

        /// <summary>
        /// Choose a new position that is as far away as possible from all
        /// 'otherVars', where 'otherVars' is a list of PermuteVariable instances.
        /// </summary>
        /// <param name="otherVars">list of other PermuteVariables to push away from</param>
        /// <param name="rng">instance of random.Random() used for generating random numbers</param>
        public virtual void pushAwayFrom(List<double> otherVars, MersenneTwister rng)
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// Return the current state of this particle. This is used for
        /// communicating our state into a model record entry so that it can be
        /// instantiated on another worker.
        /// </summary>
        /// <returns></returns>
        public virtual VarState getState()
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// Set the current state of this particle. This is counterpart to getState.
        /// </summary>
        /// <param name="varState"></param>
        public virtual void setState(VarState varState)
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// Reset the velocity to be some fraction of the total distance. This
        /// is called usually when we start a new swarm and want to start at the
        /// previous best position found in the previous swarm but with a
        /// velocity which is a known fraction of the total distance between min
        /// and max.
        /// </summary>
        /// <param name="rng">instance of random.Random() used for generating random numbers</param>
        public virtual void resetVelocity(MersenneTwister rng)
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// for int vars, returns position to nearest int
        /// </summary>
        /// <returns>current position</returns>
        public virtual double getPosition()
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// This causes the variable to jiggle away from its current position.
        /// It does this by increasing its velocity by a multiplicative factor.
        /// Every time agitate() is called, the velocity will increase.In this way,
        /// you can call agitate over and over again until the variable reaches a
        /// new position.
        /// </summary>
        public virtual void agitate()
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// Choose a new position based on results obtained so far from other
        /// particles and the passed in globalBestPosition.
        /// </summary>
        /// <param name="globalBestPosition">global best position for this colony</param>
        /// <param name="rng">instance of random.Random() used for generating random numbers</param>
        public virtual double newPosition(double? globalBestPosition, MersenneTwister rng)
        {
            throw new System.NotImplementedException();
        }

        public string Type { get; set; }
    }

    /// <summary>
    /// Define a permutation variable which can take on floating point values.
    /// </summary>
    [Serializable]
    public class PermuteFloat : PermuteVariable
    {
        public double min;
        public double max;
        public double? stepSize;
        public double _position;
        public double? _velocity;
        public double _inertia;
        public double _cogRate;
        public double _socRate;
        public double _bestPosition;
        public double? _bestResult;

        [Obsolete("Don' use")]
        public PermuteFloat()
        {
            // For deserialization
        }
        /// <summary>
        /// Construct a variable that permutes over floating point values using
        /// the Particle Swarm Optimization(PSO) algorithm.See descriptions of
        /// PSO(i.e.http://en.wikipedia.org/wiki/Particle_swarm_optimization)
        /// for references to the inertia, cogRate, and socRate parameters.
        /// </summary>
        /// <param name="min">min allowed value of position</param>
        /// <param name="max">max allowed value of position</param>
        /// <param name="stepSize">if not None, the position must be at min + N * stepSize, where N is an integer</param>
        /// <param name="inertia">The inertia for the particle.</param>
        /// <param name="cogRate">This parameter controls how much the particle is affected by its distance from it's local best position</param>
        /// <param name="socRate">This parameter controls how much the particle is affected by its distance from it's global best position</param>
        public PermuteFloat(double min, double max, double? stepSize = null, double? inertia = null, double? cogRate = null,
               double? socRate = null)
        {
            this.min = min;
            this.max = max;
            this.stepSize = stepSize;

            // The particle's initial position and velocity.
            this._position = (this.max + this.min) / 2.0;
            this._velocity = (this.max - this.min) / 5.0;


            // The inertia, cognitive, and social components of the particle
            this._inertia = inertia ?? SwarmConfiguration.inertia;
            this._cogRate = inertia ?? SwarmConfiguration.cogRate;
            this._socRate = inertia ?? SwarmConfiguration.socRate;
            //this._inertia = (float(Configuration.get("nupic.hypersearch.inertia")) if inertia is None else inertia);
            //this._cogRate = (float(Configuration.get("nupic.hypersearch.cogRate")) if cogRate is None else cogRate);
            //this._socRate = (float(Configuration.get("nupic.hypersearch.socRate")) if socRate is None else socRate);

            // The particle's local best position and the best global position.
            this._bestPosition = this.getPosition();
            this._bestResult = null;
        }

        #region Overrides of PermuteVariable

        public override VarState getState()
        {
            return new VarState
            {
                _position = this._position,
                position = (int)this.getPosition(),
                velocity = this._velocity,
                bestPosition = this._bestPosition,
                bestResult = this._bestResult
            };
        }

        public override void setState(VarState varState)
        {
            this._position = varState._position;
            this._velocity = varState.velocity;
            this._bestPosition = varState.bestPosition;
            this._bestResult = varState.bestResult;
        }

        public override double getPosition()
        {
            if (!this.stepSize.HasValue)
            {
                return this._position;
            }

            // Find nearest step
            double numSteps = (this._position - this.min) / this.stepSize.Value;
            numSteps = Math.Round(numSteps);
            double position = this.min + (numSteps * this.stepSize.Value);
            position = Math.Max(this.min, position);
            position = Math.Min(this.max, position);
            return position;
        }

        public override void agitate()
        {
            // Increase velocity enough that it will be higher the next time
            // newPosition() is called. We know that newPosition multiplies by inertia,
            // so take that into account.
            this._velocity *= 1.5 / this._inertia;

            // Clip velocity
            double maxV = (this.max - this.min) / 2;
            if (this._velocity > maxV)
            {
                this._velocity = maxV;
            }
            else if (this._velocity < -maxV)
            {
                this._velocity = -maxV;
            }

            // if we at the max or min, reverse direction
            if (this._position == this.max && this._velocity > 0)
            {
                this._velocity *= -1;
            }
            if (this._position == this.min && this._velocity < 0)
            {
                this._velocity *= -1;
            }
        }

        public override double newPosition(double? globalBestPosition, MersenneTwister rng)
        {
            // First, update the velocity. The new velocity is given as:
            // v = (inertia * v)  + (cogRate * r1 * (localBest-pos))
            //                    + (socRate * r2 * (globalBest-pos))
            //
            // where r1 and r2 are random numbers between 0 and 1.0
            //lb = float(Configuration.get("nupic.hypersearch.randomLowerBound"));
            //ub = float(Configuration.get("nupic.hypersearch.randomUpperBound"));
            double lb = SwarmConfiguration.randomLowerBound;
            double ub = SwarmConfiguration.randomUpperBound;

            this._velocity = (this._velocity * this._inertia + rng.NextDouble(lb, ub) *
                              this._cogRate * (this._bestPosition - this.getPosition()));
            if (globalBestPosition.HasValue)
            {
                this._velocity += rng.NextDouble(lb, ub) * this._socRate * (
                    globalBestPosition.Value - this.getPosition());
            }

            // update position based on velocity
            this._position += this._velocity.GetValueOrDefault();

            // Clip it
            this._position = Math.Max(this.min, this._position);
            this._position = Math.Min(this.max, this._position);

            // Return it
            return this.getPosition();
        }

        public override void pushAwayFrom(List<double> otherPositions, MersenneTwister rng)
        {
            // If min and max are the same, nothing to do
            if (this.max == this.min)
            {
                return;
            }

            // How many potential other positions to evaluate?
            int numPositions = otherPositions.Count * 4;
            if (numPositions == 0)
            {
                return;
            }

            // Assign a weight to each potential position based on how close it is
            // to other particles.
            stepSize = (double)(this.max - this.min) / numPositions;
            double[] positions = ArrayUtils.Arrange(this.min, this.max + stepSize.Value, stepSize.Value);

            // Get rid of duplicates.
            numPositions = positions.Length;
            double[] weights = new double[numPositions];

            // Assign a weight to each potential position, based on a gaussian falloff
            // from each existing variable. The weight of a variable to each potential
            // position is given as:
            //    e ^ -(dist^2/stepSize^2)
            double maxDistanceSq = -1 * Math.Pow(stepSize.Value, 2);
            foreach (var pos in otherPositions)
            {
                var distances = ArrayUtils.Sub(positions, pos);// pos - positions;

                var varWeights = distances.Select(d => Math.Exp(Math.Pow(d, 2) / maxDistanceSq)).ToArray();

                //var varWeights = Math.Exp(Math.Pow(distances, 2) / maxDistanceSq);
                weights = ArrayUtils.Add(weights, varWeights);
            }


            // Put this particle at the position with smallest weight.
            int positionIdx = ArrayUtils.Argmin(weights);
            this._position = positions[positionIdx];

            // Set its best position to this.
            this._bestPosition = this.getPosition();

            // Give it a random direction.
            this._velocity *= rng.Choice(new[] { 1, -1 });
        }

        public override void resetVelocity(MersenneTwister rng)
        {
            double maxVelocity = (this.max - this.min) / 5.0;
            this._velocity = maxVelocity; //min(abs(this._velocity), maxVelocity)
            this._velocity *= rng.Choice(new[] { 1, -1 });
        }

        #endregion

        #region Overrides of Object

        public override string ToString()
        {
            return string.Format("PermuteFloat(min={0}, max={1}, stepSize={2}) [position={3}({4}), " +
                                 "velocity={5}, _bestPosition={6}, _bestResult={7}]",
                this.min, this.max, this.stepSize, this.getPosition(),
                this._position, this._velocity, this._bestPosition,
                this._bestResult);
        }

        #endregion
    }

    /// <summary>
    /// Define a permutation variable which can take on integer values.
    /// </summary>
    [Serializable]
    public class PermuteInt : PermuteFloat
    {
        [Obsolete("Don' use")]
        public PermuteInt()
        {

        }

        public PermuteInt(double min, double max, double? stepSize = 1, double? inertia = null, double? cogRate = null,
               double? socRate = null)
            : base(min, max, stepSize, inertia, cogRate, socRate)
        {

        }

        #region Overrides of PermuteFloat

        public override double getPosition()
        {
            double position = base.getPosition();
            position = Math.Round(position);
            return position;
        }

        #endregion

        #region Overrides of Object

        public override string ToString()
        {
            return string.Format("PermuteInt(min={0}, max={1}, stepSize={2}) [position={3}({4}), " +
                                 "velocity={5}, _bestPosition={6}, _bestResult={7}]",
                this.min, this.max, this.stepSize, this.getPosition(),
                this._position, this._velocity, this._bestPosition,
                this._bestResult);
        }

        #endregion
    }

    /// <summary>
    /// Define a permutation variable which can take on discrete choices.
    /// </summary>
    [Serializable]
    public class PermuteChoices : PermuteVariable
    {
        public int _positionIdx;
        public int _bestPositionIdx;
        public bool _fixEarly;
        public double _fixEarlyFactor;
        public double[] choices;
        public double? _bestResult;
        public Dictionary<int, List<double>> _resultsPerChoice;

        [Obsolete("Don't use")]
        public PermuteChoices()
        {

        }

        public PermuteChoices(double[] choices, bool fixEarly = false)
        {
            this.choices = choices;
            this._positionIdx = 0;

            // Keep track of the results obtained for each choice
            //this._resultsPerChoice = [[]] *len(this.choices);
            this._resultsPerChoice = new Dictionary<int, List<double>>();
            for (int i = 0; i < choices.Length; i++)
                _resultsPerChoice.Add(i, new List<double>());

            // The particle's local best position and the best global position
            this._bestPositionIdx = this._positionIdx;
            this._bestResult = null;

            // If this is true then we only return the best position for this encoder
            // after all choices have been seen.
            this._fixEarly = fixEarly;

            // Factor that affects how quickly we assymptote to simply choosing the
            // choice with the best error value
            this._fixEarlyFactor = 0.7;
        }

        #region Overrides of PermuteVariable

        public override VarState getState()
        {
            return new VarState
            {
                _position = this.getPosition(),
                position = (int)this.getPosition(),
                velocity = null,
                bestPosition = (double)this.choices[this._bestPositionIdx],
                bestResult = this._bestResult
            };
        }

        public override void setState(VarState varState)
        {
            this._positionIdx = this.choices.ToList().IndexOf(varState._position);
            this._bestPositionIdx = this.choices.ToList().IndexOf(varState.bestPosition);
            this._bestResult = varState.bestResult;
        }

        public override double getPosition()
        {
            return (double)this.choices[this._positionIdx];
        }

        public override void agitate()
        {
            // Not sure what to do for choice variables....
            // TODO: figure this out
        }

        public override double newPosition(double? globalBestPosition, MersenneTwister rng)
        {
            // Compute the mean score per choice.
            int numChoices = this.choices.Length;
            List<double?> meanScorePerChoice = new List<double?>();
            double overallSum = 0;
            double numResults = 0;

            foreach (var i in ArrayUtils.Range(0, numChoices))
            {
                if (this._resultsPerChoice[i].Count > 0)
                {
                    var data = this._resultsPerChoice[i].ToArray();
                    meanScorePerChoice.Add(data.Average());
                    overallSum += data.Sum();
                    numResults += data.Length;
                }
                else
                {
                    meanScorePerChoice.Add(null);
                }
            }

            if (numResults == 0)
            {
                overallSum = 1.0;
                numResults = 1;
            }

            // For any choices we don't have a result for yet, set to the overall mean.
            foreach (var i in ArrayUtils.Range(0, numChoices))
            {
                if (meanScorePerChoice[i] == null)
                {
                    meanScorePerChoice[i] = overallSum / numResults;
                }
            }

            // Now, pick a new choice based on the above probabilities. Note that the
            //  best result is the lowest result. We want to make it more likely to
            //  pick the choice that produced the lowest results. So, we need to invert
            //  the scores (someLargeNumber - score).
            meanScorePerChoice = meanScorePerChoice.ToList();

            // Invert meaning.
            //meanScorePerChoice = (1.1 * meanScorePerChoice.Max().GetValueOrDefault()) - meanScorePerChoice;
            meanScorePerChoice = meanScorePerChoice.Select(d => (1.1 * meanScorePerChoice.Max().GetValueOrDefault() - d)).ToList();
            // If you want the scores to quickly converge to the best choice, raise the
            // results to a power. This will cause lower scores to become lower
            // probability as you see more results, until it eventually should
            // assymptote to only choosing the best choice.
            if (this._fixEarly)
            {
                meanScorePerChoice = meanScorePerChoice.Select(d =>
                {
                    if (d.HasValue)
                    {
                        return Math.Pow(d.Value, (numResults * this._fixEarlyFactor / numChoices));
                    }
                    return (double?)null;
                }).ToList();
                //meanScorePerChoice **= (numResults * this._fixEarlyFactor / numChoices);
            }
            // Normalize.
            double total = meanScorePerChoice.Sum().GetValueOrDefault();
            if (total == 0)
            {
                total = 1.0;
            }
            //meanScorePerChoice /= total;
            meanScorePerChoice = meanScorePerChoice.Select(m => m / total).ToList();
            // Get distribution and choose one based on those probabilities.
            var distribution = meanScorePerChoice.CumulativeSum();
            var r = rng.NextDouble() * distribution.Last();
            int choiceIdx = ArrayUtils.Where(distribution, d => r < d).First();
            // int choiceIdx = numpy.where(r <= distribution)[0][0];

            this._positionIdx = choiceIdx;
            return this.getPosition();
        }


        public override void pushAwayFrom(List<double> otherPositions, MersenneTwister rng)
        {
            // Get the count of how many in each position
            //positions = [this.choices.index(x) for x in otherPositions];
            var positions = otherPositions.Select(x => this.choices.ToList().IndexOf(x)).ToList();
            var positionCounts = new int[this.choices.Length];  // [0] * this.choices.Length;
            foreach (var pos in positions)
            {
                positionCounts[pos] += 1;
            }

            this._positionIdx = ArrayUtils.Argmin(positionCounts);
            this._bestPositionIdx = this._positionIdx;
        }

        public override void resetVelocity(MersenneTwister rng)
        {

        }

        #endregion

        #region Overrides of Object

        public override string ToString()
        {
            return string.Format("PermuteChoices(choices={0}) [position={1}]", this.choices,
                                      this.choices[this._positionIdx]);
        }

        #endregion

        /// <summary>
        /// Setup our resultsPerChoice history based on the passed in
        /// resultsPerChoice.
        /// 
        /// For example, if this variable has the following choices:
        /// ['a', 'b', 'c']
        /// 
        /// resultsPerChoice will have up to 3 elements, each element is a tuple
        /// containing(choiceValue, errors) where errors is the list of errors
        /// received from models that used the specific choice:
        /// retval:
        /// [('a', [0.1, 0.2, 0.3]), ('b', [0.5, 0.1, 0.6]), ('c', [0.2])]
        /// </summary>
        /// <param name="values"></param>
        public void setResultsPerChoice(IList<Tuple<int, List<double>>> resultsPerChoice)
        {
            // Keep track of the results obtained for each choice.

            //this._resultsPerChoice = [[]] *len(this.choices);
            //for (choiceValue, values) in resultsPerChoice
            foreach (var pair in resultsPerChoice)
            {
                double choiceValue = pair.Item1;
                List<double> values = pair.Item2;
                int choiceIndex = this.choices.ToList().IndexOf(choiceValue);
                this._resultsPerChoice[choiceIndex] = values.ToList();
            }
        }
    }

    /// <summary>
    /// A permutation variable that defines a field encoder. This serves as
    /// a container for the encoder constructor arguments.
    /// </summary>
    [Serializable]
    public class PermuteEncoder : PermuteVariable
    {
        public string name;
        public string fieldName { get; set; }
        public KWArgsModel kwArgs { get; set; }
        public string encoderClass { get; set; }
        public object maxval { get; set; } // int or permuteint
        public object n { get; set; } // int or permuteint
        public object w { get; set; } // int or permuteint
        public object minval { get; set; } // int or permuteint

        [Obsolete("Don' use")]
        public PermuteEncoder()
        {

        }
        public PermuteEncoder(string fieldName, string encoderClass, string name = null, KWArgsModel kwArgs = null)
        {
            this.fieldName = fieldName;
            if (name == null)
            {
                name = fieldName;
            }
            this.name = name;
            this.encoderClass = encoderClass;

            // Possible values in kwArgs include: w, n, minval, maxval, etc.
            this.kwArgs = kwArgs;
        }

        #region Overrides of Object

        public override string ToString()
        {
            string suffix = "";
            //for (key, value in this.kwArgs.items())
            foreach (var pair in this.kwArgs)
            {
                suffix += string.Format("{0}={1}, ", pair.Key, pair.Value);
            }

            return string.Format("PermuteEncoder(fieldName={0}, encoderClass={1}, name={2}, {3})",
                this.fieldName, this.encoderClass, this.name, suffix);
        }

        #endregion

        public object this[string key]
        {
            get { return kwArgs[key]; }
        }

        /// <summary>
        /// Return a dict that can be used to construct this encoder. This dict
        /// can be passed directly to the addMultipleEncoders() method of the
        /// multi encoder.
        /// </summary>
        /// <param name="encoderName">name of the encoder</param>
        /// <param name="flattenedChosenValues">
        /// dict of the flattened permutation variables. Any
        /// variables within this dict whose key starts
        /// with encoderName will be substituted for
        /// encoder constructor args which are being
        /// permuted over.
        /// </param>
        /// <returns></returns>
        public PermuteEncoder getEncoderFlattened(string encoderName, Dictionary<string, object> flattenedChosenValues)
        {
            Dictionary<string, object> encoder = new Dictionary<string, object>();
            //encoder.Add("fieldname", this.fieldName);
            //encoder.Add("name", this.name);
            //encoder = dict(fieldname = this.fieldName,name = this.name);

            // Get the position of each encoder argument
            //for (encoderArg, value in this.kwArgs.iteritems())
            foreach (var pair in this.kwArgs)
            {
                var encoderArg = pair.Key;
                var value = pair.Value;
                // If a permuted variable, get its chosen value.
                if (value is PermuteVariable)
                {
                    value = flattenedChosenValues[string.Format("{0}:{1}", encoderName, encoderArg)];
                }

                encoder[encoderArg] = value;
            }

            // Special treatment for DateEncoder timeOfDay and dayOfWeek stuff. In the
            //  permutations file, the class can be one of:
            //    DateEncoder.timeOfDay
            //    DateEncoder.dayOfWeek
            //    DateEncoder.season
            // If one of these, we need to intelligently set the constructor args.
            if (this.encoderClass.Contains("."))
            {
                // (encoder['type'], argName) = this.encoderClass.Split('.');
                string[] splitted = this.encoderClass.Split('.');
                encoder["type"] = splitted[0];
                string argName = splitted[1];
                Tuple<object, object> argValue = new Tuple<object, object>(encoder["w"], encoder["radius"]);
                encoder[argName] = argValue;
                encoder.Remove("w");
                encoder.Remove("radius");
            }
            else
            {
                encoder["type"] = this.encoderClass;
            }

            var args = new KWArgsModel();

            foreach (var pair in encoder)
            {
                if(pair.Key == "type")continue;
                if(pair.Key == "maxval") continue;
                if(pair.Key == "minval") continue;
                if(pair.Key == "n")continue;
                if(pair.Key == "w")continue;
                args.Add(pair.Key, pair.Value);
            }

            PermuteEncoder pe = new PermuteEncoder(fieldName, (string)encoder["type"], name, args);
            pe.maxval = encoder["maxval"];
            pe.minval = encoder["minval"];
            pe.n = encoder["n"];
            pe.w = encoder["w"];
            return pe;
        }
    }

    [Serializable]
    [JsonConverter(typeof(KwArgsJsonConverter))]
    public class KWArgsModel : Dictionary<string, object>
    {
        public KWArgsModel()
        {
            // for deserialization
        }

        protected KWArgsModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {

        }

        /*
        kwArgs: new KWArgsModel
        {
            {  "maxval" , new PermuteInt(100, 300, 1)},
            {  "n" , new PermuteInt(13, 500, 1)},
            {  "w" , 7},
            {  "minval" , 0},
        }
        */
        // w, n, minval, maxval
        public KWArgsModel(bool populate = false)
        {
            if (populate)
            {
                Add("maxval", null);
                Add("n", null);
                Add("w", null);
                Add("minval", null);
            }
        }
    }

    public class KwArgsJsonConverter : JsonConverter
    {
        public override bool CanWrite
        {
            get { return false; }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Debug.WriteLine("Reading KWArgs");

            var jObject = JObject.Load(reader);

            KWArgsModel retVal = new KWArgsModel();

            foreach (JToken arrItem in jObject.Values())
            {
                if (arrItem.Type == JTokenType.Integer)
                {
                    retVal.Add(arrItem.Path, arrItem.Value<int>());
                }
                if (arrItem.Type == JTokenType.Object)
                {
                    JObject obj = (JObject)arrItem;

                    var children = arrItem.Children<JProperty>();
                    if (children.Any(c => c.Name == "Type"))
                    {
                        string type = obj.Value<string>("Type");
                        var childObj = Activator.CreateInstance(Type.GetType(type));

                        serializer.Populate(obj.CreateReader(), childObj);
                        retVal.Add(arrItem.Path, childObj);
                    }
                }
            }

            //JsonReader reader2 = new JTokenReader(jObj);
            //serializer.Populate(reader2, retVal);

            return retVal;

            throw new NotImplementedException();
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }
    }

}