# IsIdentifiable

## Contents

1.  [Overview](#overview)
1.  [Setup](#setup)
1.  [Optional Downloads](#optional-downloads)
1.  [NLP](#nlp)
    1. [SpaCy Classifier](#spacy-classifier)
    2. [Stanford Classifier](#stanford-classifier)
1.  [Invocation](#invocation)
1.  [Examples](#examples)
1.  [Rules](#rules)
    1. [Basic Rules](#basic-rules)
    2. [Socket Rules](#socket-rules)
    3. [Consensus Rules](#consensus-rules)
    4. [Allow List Rules](#allow-list-rules)
1.  [Class Diagram](#class-diagram)

## Overview

This library evaluates 'data' for personally identifiable values (e.g. names). It can source data from a veriety of places (e.g. databases, file system).

## Setup

To run IsIdentifiable you must first build the [ii] tool then download the required data models for NER and OCR.

Rules must be placed into a suitable directory.

### Optional Downloads

The following optional download expand the capabilities of the software:

| File                                                                                                            | Destination           | Windows Script                                    | Linux Script                                    |
| --------------------------------------------------------------------------------------------------------------- | --------------------- | ------------------------------------------------- | ----------------------------------------------- |
| [Tesseract Data files (pixel OCR models)](https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata)\* | `./data/tessdata`     | [download.ps1](../data/tessdata/download.ps1)     | [download.sh](../data/tessdata/download.sh)     |
| [Stanford NER Classifiers](http://nlp.stanford.edu/software/stanford-ner-2016-10-31.zip)\*\*                    | `./data/stanford-ner` | [download.ps1](../data/stanford-ner/download.ps1) | [download.sh](../data/stanford-ner/download.sh) |

_\*Only required for DICOM pixel text detection_
_\*\*Only required for NLP NERDaemon_

## NLP

IsIdentifiable has some basic rules based on regular expressions (Postcode, Dates etc) but to
get the most out of it you will want to use one of the Natural Language Processing (NLP) daemons.

There are 2 daemons supplied but it is easy to write your own. Daemons listen on a local or remote
port and are passed data for classification as it is streamed.

After starting the classifier(s) you must configure either a [Socket Rule](#socket-rules) or [Consensus Rule](#consensus-rules)

### SpaCy Classifier

To use the SpaCy classifier you will need Python 3 and the SpaCy library

```
sudo apt-get install python3.9
pip install -U spacy
pip install pyyaml
```

Next run [ner_daemon_spacy.py](../nlp/nerd-spacy/ner_daemon_spacy.py). Use the `-d` flag the first time to fetch the language model:

```
cd ./nlp/
python3 ./ner_daemon_spacy.py -d en_core_web_md
```

After the download completes you can start the NLP by without `-d`. By default this process will block the console
but you can start the process detached with the `&` operator:

```
python3 ./ner_daemon_spacy.py &
```

You can test that the service is running with the test script:

```
python3 ./test_ner_daemon_spacy.py
```

The default listening port for the script is `1882` but you can specify a different one with `-p someport`

### Stanford Classifier

The second classifier provided out of the box is a wrapper for [Stanford NER](https://nlp.stanford.edu/software/CRF-NER.html).

To use this classifier download and unzip the latest `nerd` binary from the [IsIdentifiable GitHub Releases](https://github.com/SMI/IsIdentifiable/releases/). You will also need to install the Java runtime.

Start the service with:

```
./nerd
```

_Add & at the end to detach the console (prevents blocking)_

This classifier listens on port `1881`

## Invocation

IsIdentifiable can be run from the [ii] command line tool:

-   To process a DICOM file or a directory of DICOM files
-   To process a every row of every column in a database table

You can also link your code to the [nuget package](https://www.nuget.org/packages/IsIdentifiable/). For example to add a new input type or operate as a service that evaluates data on demand.

### Examples

See [ii Usage Examples](../ii/README.md#Examples)

## Rules

Rules can be used to customise the way failures are handled.
A failure is a fragment of text (or image) which contains identifiable data.
It can either be ignored (because it is a false positive) or reported.

Some rules come out of the box (e.g. CHI/Postcode/Date) but for the rest you must configure rules in a rules.yaml file.
There are three classes of rule: BasicRules, SocketRules and AllowlistRules. See below for more details of each.
They are applied in that order, so if a value is Ignored in a Basic rule it will not be passed to any further rules.
If a value fails in a SocketRule (eg. the NER daemon labels it as a Person), then a subsequent Allowlist rule can Ignore it.
Not all Ignore rules go into the AllowlistRules section; this is intended only for allow-listing fragments which NERd has incorrectly reported as failures.

Rules can be read from one or more yaml files. Each file can have zero or one set of BasicRules, plus zero or one set of AllowlistRules.
All of the BasicRules from all of the files will be merged to form a single set of BasicRules; similarly for AllowlistRules.

When running in service mode (as a microservice host) the rules are read from all `*.yaml` files found in the `IsIdentifiableRules` directory inside the data directory. The data directory path is defined in the program yaml file (which was specified with -y) using the key `IsIdentifiableOptions|DataDirectory`.

When running in file or database mode the yaml file option -y is not used so the command line option `--rulesdirectory` needs to be specified. This should be the path to the IsIdentifiableRules directory.

### Basic Rules

These can either result in a value being Reported or Ignored (i.e. not passed to any downstream classifiers). Rules can apply to all columns (e.g. Ignore the Modality column) or only those values that match a Regex. The regex is specified using `IfPattern`. Regex case sensitivity is determined by the `CaseSensitive` property (defaults to false).

```yaml
BasicRules:
    # Report any values which contain 2 digits as a PrivateIdentifier
    - IfPattern: "[0-9][0-9]"
      Action: Report
      As: PrivateIdentifier

    # Do not run any classifiers on the Modality column
    - Action: Ignore
      IfColumn: Modality
```

### Socket Rules

You can outsource the classification to separate application(s) (e.g. NERDaemon) by adding `Socket Rules`

```yaml
SocketRules:
    - Host: 127.0.123.123
      Port: 1234
```

The TCP protocol starts with IsIdentifiable sending the word for classification i.e.

```
Sender: word or sentence\0
```

The service is expected to respond with 0 or more classifications of bits in the word that are problematic. These take the format:

```
Responder: Classification\0Offset\0Offending Word(s)\0
```

Once the responder has decided there are no more offending sections (or there were none to begin with) it sends a double null terminator. This indicates that the original word or sentence has been fully processed and the Sender can send the next value requiring validation.

```
Responder: \0\0
```

### Consensus Rules

If you have two or more rules that you want to cooperate when determining whether data is identifiable or not e.g. 2 NLP Name Entity Recognizers you can use a ConsensusRule. These rules require all subrules to agree on whether to Report or Ignore a given cell value. If there is disagreement then the rule is not applied (`RuleAction.None` i.e. take no action).

You can configure a consensus rule using the following yaml:

```yaml
ConsensusRules:
    - Rules:
          - !SocketRule
            Host: 127.0.123.123
            Port: 1234
          - !SocketRule
            Host: 127.0.123.123
            Port: 567
```

Consensus rules are specifically designed for intersecting two or more over matching rules e.g. NLP classifications. If only one rule flags something as identifiable it will be ignored (both must agree).

### Allow List Rules

Allow list rules are a last chance filter on the final output of all other rules. They allow discarding rules based on the whole string or the specific failing part.

The Action for a Allow List rule must be Ignore because it is intended to allow values previously reported to be ignored as false positives.

All of the constraints must match in order for the rule to Ignore the value.

As soon as a value matches an allow list rule no further allow list rules are needed.
Unlike a BasicRule whose Pattern matches the full value of a field (column or DICOM tag) the Allowlist rule has two Patterns, IfPattern which has the same behaviour and IfPartPattern which matches only the substring that failed. This feature allows context to be specified, see the second example below.
A Allowlist rule can also match the failure classification (`PrivateIdentifier`, `Location`, `Person`, `Organization`, `Money`, `Percent`, `Date`, `Time`, `PixelText`, `Postcode`).
For example, if SIEMENS has been reported as a Person found in the the Manufacturer column,

```yaml
AllowlistRules:
    - Action: Ignore
      As: Person
      IfColumn: Manufacturer
      IfPartPattern: ^SIEMENS$
```

For example, what seems like a name Brian can be ignored if it occurs in the exact phrase "MR Brian And Skull" using:

```yaml
IfPartPattern: ^Brian$
IfPattern: MR Brian And Skull
```

Note that there is no need to specify ^ and $ in IfPattern as other text before or after it will not change the meaning.

<!-- Links -->

[ii]: ../ii/README.md
