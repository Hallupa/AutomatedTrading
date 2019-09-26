######################## Imports ########################
import warnings
import pathlib
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.image as mpimg
import os

# Hide all 'FutureWarning'
with warnings.catch_warnings():  
    warnings.filterwarnings("ignore",category=FutureWarning)
    import tensorflow as tf
    from tensorflow import keras
AUTOTUNE = tf.data.experimental.AUTOTUNE
#########################################################

# numpy.ndarray of 28, 28
def getMNISTDataSet():
    mnist = tf.keras.datasets.mnist
    (x_train, y_train), (x_test, y_test) = mnist.load_data()
    x_train, x_test = x_train / 255.0, x_test / 255.0
    return (x_train, y_train), (x_test, y_test)

def getRawDataSet(directory):
    valuesCollection=[]
    labelsStrings=[]
    labels=[]

    # Get raw path dataset
    all_paths = [str(path) for path in pathlib.Path(directory).glob('*.csv')]

    for path in all_paths:
        with open(path, 'r') as file:
            string_values = file.read().split(',')
            numeric_values = [float(string_value) for string_value in string_values[1:]]
            valuesCollection.append(numeric_values)
            labels.append(int(string_values[0]))
        labelsStrings.append(os.path.basename(path).split("_")[0])

    print("######## Got ", len(labelsStrings), "paths from ", directory)

    print("Getting NumPy arrays")
    #labels = StringsToInts(labelsStrings)
    x_train = np.array(valuesCollection)
    y_train = np.array(labels)

    print("Returning data")
    ratioForXTrain = 1.0 # 0.75
    train_num = (int)(len(x_train) * ratioForXTrain)
    return (x_train[:train_num], y_train[:train_num]),(x_train[train_num:], y_train[train_num:])

def getJpegDataSet(directory):
    images=[]
    labelsStrings=[]
    # Get image path dataset
    all_paths = [str(path) for path in pathlib.Path(directory).glob('*.png')]

    tf.InteractiveSession()
    for path in all_paths:
        image = tf.image.decode_png(tf.io.read_file(path), channels=3)
        #image = tf.image.resize(image, [192, 192]) # All images are the same size - is this needed ??
        #image /= 255.0  # normalize to [0,1] range

        nd_image = image.eval()

        images.append(nd_image)
        labelsStrings.append(os.path.basename(path).split("_")[0])

    labels = StringsToInts(labelsStrings)

    print("Getting NumPy arrays")
    x_train = np.array(images)
    y_train = np.array(labels)
    x_train = x_train / 255.0
    train_num = (int)(len(x_train) * 0.75)

    print("Returning data")
    return (x_train[:train_num], y_train[:train_num]), (x_train[train_num:], y_train[train_num:])

def StringsToInts(labelsStrings):
    labels=[]
    labelsToIntLookup=dict()
    labelsToIntLookupNextValue = 1
    for l in labelsStrings:
        if l in labelsToIntLookup:
            labels.append(labelsToIntLookup[l])
        else:
            labelsToIntLookup[l] = labelsToIntLookupNextValue
            labels.append(labelsToIntLookup[l])
            labelsToIntLookupNextValue = labelsToIntLookupNextValue + 1
    
    for key in labelsToIntLookup.keys():
        print("Key: ", key, " =", labelsToIntLookup[key])

    return labels